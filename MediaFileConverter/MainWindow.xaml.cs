using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.IO;
using System.ComponentModel;

namespace MediaFileConverter
{
    public struct WorkerArguments
    {
        public List<string> FilesToConvert { get; set; }
        public string FFMPEGPath { get; set; }
        public string InputPath { get; set; }
        public string OutputPath { get; set; }
        public string Format { get; set; }
        public bool Overwrite { get; set; }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        List<string> formats;

        public MainWindow()
        {
            InitializeComponent();

            //Extract list of formats from FFMPEG
            formats = ExecuteFFMPEGCommandWithResult("-formats")
                                   .Split('\r')
                                   .Skip(4)
                                   .Where(s => s != "\n")
                                   .Select(s => s.Substring(5))
                                   .Select(s => s.Substring(0, s.IndexOf(' ')))
                                   .Where(s => !s.Contains(","))
                                   .ToList();

            foreach (string format in formats)
            {
                cmbFormatList.Items.Add(format);
            }

            chkIsRecursive.IsEnabled = false;
        }

        /// <summary>
        /// Executes an FFMPEG command and returns the complete output of it
        /// </summary>
        /// <param name="pCommand"></param>
        /// <returns></returns>
        string ExecuteFFMPEGCommandWithResult(string pCommand)
        {
            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = txtFfmpegPath.Text,
                Arguments = /*"/C " +*/ pCommand,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            process.StartInfo = startInfo;
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return output;
        }

        void ExecuteFFMPEGCommand(string pFFMPEGPath, string pCommand)
        {
            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                WindowStyle = ProcessWindowStyle.Minimized,
                FileName = pFFMPEGPath,
                Arguments = /*"/C " +*/ pCommand,
                RedirectStandardOutput = false,
                UseShellExecute = true
            };
            process.StartInfo = startInfo;
            process.Start();
            process.WaitForExit();
        }


        List<string> GetAllFilesRecursively(string pPath)
        {
            List<string> files = new List<string>();

            files.AddRange(Directory.GetFiles(pPath).Where(s => formats.Contains(Path.GetExtension(s).Replace(".", ""))));

            List<string> directories = Directory.GetDirectories(pPath).ToList();

            if (directories.Count > 0)
                foreach (string dir in directories)
                    files.AddRange(GetAllFilesRecursively(dir));

            return files;
        }

        void ToggleEnabledStatus(bool pIsEnabled)
        {
            txtFfmpegPath.IsEnabled = pIsEnabled;
            txtInputPath.IsEnabled = pIsEnabled;
            txtOutputPath.IsEnabled = pIsEnabled;

            chkIsFolder.IsEnabled = pIsEnabled;
            chkIsRecursive.IsEnabled = pIsEnabled;
            chkOverwrite.IsEnabled = pIsEnabled;

            btnBrowseFFMPEG.IsEnabled = pIsEnabled;
            btnBrowseInput.IsEnabled = pIsEnabled;
            btnBrowseOutput.IsEnabled = pIsEnabled;

            cmbFormatList.IsEnabled = pIsEnabled;

            btnConvert.Content = pIsEnabled ? "Convert" : "Cancel";
        }

        #region Events
        private void btnBrowseFFMPEG_Click(object sender, RoutedEventArgs e)
        {
            // Create OpenFileDialog 
            OpenFileDialog dlg = new OpenFileDialog();

            // Set filter for file extension and default file extension 
            dlg.DefaultExt = ".exe";
            dlg.Filter = "Executables|*.exe";

            // Display OpenFileDialog by calling ShowDialog method 
            Nullable<bool> result = dlg.ShowDialog();

            // Get the selected file name and display in a TextBox 
            if (result == true)
            {
                // Open document 
                string filename = dlg.FileName;
                txtFfmpegPath.Text = filename;
            }
        }

        private void btnBrowseInput_Click(object sender, RoutedEventArgs e)
        {
            if (chkIsFolder.IsChecked == false)
            {
                // Create OpenFileDialog 
                OpenFileDialog dlg = new OpenFileDialog();

                // Display OpenFileDialog by calling ShowDialog method 
                Nullable<bool> result = dlg.ShowDialog();

                // Get the selected file name and display in a TextBox 
                if (result == true)
                {
                    // Open document 
                    string filename = dlg.FileName;
                    txtInputPath.Text = filename;
                }
            }
            else
            {
                CommonOpenFileDialog dialog = new CommonOpenFileDialog();
                dialog.IsFolderPicker = true;
                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    txtInputPath.Text = dialog.FileName;
                }
            }
        }

        private void btnBrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                txtOutputPath.Text = dialog.FileName;
            }
        }

        private void btnConvert_Click(object sender, RoutedEventArgs e)
        {
            if (m_worker.IsBusy)
            {
                m_worker.CancelAsync();
                ToggleEnabledStatus(true);
                return;
            }

            if (!Directory.Exists(txtOutputPath.Text))
            {
                Directory.CreateDirectory(txtOutputPath.Text);
            }

            List<string> filesToConvert = new List<string>();

            if (txtInputPath.Text == "")
            {
                MessageBox.Show("Input is empty");
                return;
            }

            if (txtOutputPath.Text == "" || File.GetAttributes(txtOutputPath.Text).HasFlag(FileAttributes.Directory) == false)
            {
                MessageBox.Show("Output is not a folder, please enter a folder");
                return;
            }

            if (cmbFormatList.SelectedValue == null)
            {
                MessageBox.Show("Please select an output format");
                return;
            }

            // get the file attributes for file or directory
            FileAttributes attr = File.GetAttributes(txtInputPath.Text);

            if (attr.HasFlag(FileAttributes.Directory) && chkIsFolder.IsChecked == false)
            {
                MessageBoxResult result = MessageBox.Show("Input path is a folder but \"Is Folder\" is not checked.\r\nDo you wish to continue as if \"Is Folder\" was checked?", "Error I guess?", MessageBoxButton.YesNo);

                if (result != MessageBoxResult.Yes)
                    return;
            }
            else if (!attr.HasFlag(FileAttributes.Directory) && chkIsFolder.IsChecked == true)
            {
                MessageBoxResult result = MessageBox.Show("Input path is a file but \"Is Folder\" is checked.\r\nDo you wish to continue as if \"Is Folder\" was unchecked?", "Error I guess?", MessageBoxButton.YesNo);

                if (result != MessageBoxResult.Yes)
                    return;
            }

            if (attr.HasFlag(FileAttributes.Directory))
            {

                if (chkIsRecursive.IsChecked == true)
                    filesToConvert.AddRange(GetAllFilesRecursively(txtInputPath.Text));
                else
                    filesToConvert = Directory.GetFiles(txtInputPath.Text)
                                              .Where(s => formats.Contains(Path.GetExtension(s).Replace(".", "")))
                                              .ToList();
            }
            else
                filesToConvert.Add(txtInputPath.Text);

            barProgressBar.Maximum = filesToConvert.Count;
            barProgressBar.Value = 0;
            barProgressBar.UpdateLayout();

            //foreach (string file in filesToConvert)
            //{
            //    string outputFile = "";

            //    if (attr.HasFlag(FileAttributes.Directory))
            //        outputFile = txtOutputPath.Text + String.Join(".", file.Replace(txtInputPath.Text, "").Split('.').Reverse().Skip(1).Reverse()) + "." + cmbFormatList.SelectedValue;
            //    else
            //        outputFile = txtOutputPath.Text + "\\" + Path.GetFileNameWithoutExtension(file) + "." + cmbFormatList.SelectedValue;

            //    if (!(chkOverwrite.IsChecked == false && File.Exists(outputFile)))
            //    {
            //        if (File.Exists(outputFile))
            //            File.Delete(outputFile);

            //        ExecuteFFMPEGCommand("-i \"" + file + "\" \"" + outputFile + "\"");
            //    }
            //}

            m_worker = new BackgroundWorker();
            m_worker.WorkerReportsProgress = true;
            m_worker.WorkerSupportsCancellation = true;
            m_worker.DoWork += Worker_DoWork;
            m_worker.ProgressChanged += Worker_ProgressChanged;
            m_worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
            m_worker.RunWorkerAsync(new WorkerArguments()
            {
                FilesToConvert = filesToConvert,
                FFMPEGPath = txtFfmpegPath.Text,
                InputPath = txtInputPath.Text,
                OutputPath = txtOutputPath.Text,
                Format = cmbFormatList.SelectedValue.ToString(),
                Overwrite = chkOverwrite.IsChecked == true
            });

            ToggleEnabledStatus(false);
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            ToggleEnabledStatus(true);
        }

        BackgroundWorker m_worker = new BackgroundWorker();

        private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            barProgressBar.Value += e.ProgressPercentage;
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            WorkerArguments args = (WorkerArguments)e.Argument;

            // get the file attributes for file or directory
            FileAttributes attr = File.GetAttributes(args.InputPath);

            foreach (string file in args.FilesToConvert)
            {
                if (m_worker.CancellationPending)
                    return;

                string outputFile = "";

                if (attr.HasFlag(FileAttributes.Directory))
                    outputFile = args.OutputPath + String.Join(".", file.Replace(args.InputPath, "").Split('.').Reverse().Skip(1).Reverse()) + "." + args.Format;
                else
                    outputFile = args.OutputPath + "\\" + Path.GetFileNameWithoutExtension(file) + "." + args.Format;

                if ((args.Overwrite == false && File.Exists(outputFile) == false) ||
                    (args.Overwrite == true && File.Exists(outputFile) == true))
                {
                    if (File.Exists(outputFile))
                        File.Delete(outputFile);

                    ExecuteFFMPEGCommand(args.FFMPEGPath, "-i \"" + file + "\" \"" + outputFile + "\"");
                }
                (sender as BackgroundWorker).ReportProgress(1);
            }
        }

        private void chkIsFolder_Checked(object sender, RoutedEventArgs e)
        {
            chkIsRecursive.IsEnabled = true;
        }

        private void chkIsFolder_Unchecked(object sender, RoutedEventArgs e)
        {
            chkIsRecursive.IsEnabled = false;
        }
        #endregion

        private void btnRename_Click(object sender, RoutedEventArgs e)
        {
            List<string> files = Directory.GetFiles(txtInputPath.Text).ToList();
            int i = 0;
            foreach (string f in files)
            {
                File.Copy(f, txtOutputPath.Text + "\\" + txtNewName.Text.ToUpper() + i++ + ".WAV");
            }
        }
    }
}
