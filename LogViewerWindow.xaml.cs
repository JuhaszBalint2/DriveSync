using System;
using System.Collections.Generic;
using System.Windows;
using Microsoft.Win32;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace DriveSync.WPF.Views
{
    public partial class LogViewerWindow : Window
    {
        private readonly string _rawLog;
        private readonly DateTime _timestamp;

        public LogViewerWindow(string log, DateTime timestamp)
        {
            InitializeComponent();
            _rawLog = log;
            _timestamp = timestamp;

            Debug.WriteLine($"Raw log length: {_rawLog?.Length ?? 0}");
            Debug.WriteLine("First 100 characters of raw log:");
            Debug.WriteLine(_rawLog?.Substring(0, Math.Min(100, _rawLog?.Length ?? 0)));

            this.Loaded += Window_Loaded;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            DisplayLog();
        }

        private string ParseLogLine(string line, ref int operationCount)
        {
            // Trim the line to remove any leading/trailing whitespace
            line = line.Trim();

            // If the line is empty, return null
            if (string.IsNullOrEmpty(line)) return null;

            // Regular expressions to match different log message types
            var copyRegex = new Regex(@"COPY:\s*(.+)\s*->\s*(.+)", RegexOptions.IgnoreCase);
            var deleteRegex = new Regex(@"DELETE:\s*(.+)", RegexOptions.IgnoreCase);
            var createRegex = new Regex(@"CREATE:\s*(.+)", RegexOptions.IgnoreCase);
            var moveRegex = new Regex(@"MOVE:\s*(.+)\s*->\s*(.+)", RegexOptions.IgnoreCase);
            var errorRegex = new Regex(@"ERROR:\s*(.+)", RegexOptions.IgnoreCase);
            var warningRegex = new Regex(@"WARNING:\s*(.+)", RegexOptions.IgnoreCase);

            // Check for different types of log messages and format accordingly
            Match copyMatch = copyRegex.Match(line);
            Match deleteMatch = deleteRegex.Match(line);
            Match createMatch = createRegex.Match(line);
            Match moveMatch = moveRegex.Match(line);
            Match errorMatch = errorRegex.Match(line);
            Match warningMatch = warningRegex.Match(line);

            string formattedLine = null;

            if (copyMatch.Success)
            {
                operationCount++;
                formattedLine = $"📋 Copied: \n  From: {copyMatch.Groups[1].Value}\n  To: {copyMatch.Groups[2].Value}";
            }
            else if (deleteMatch.Success)
            {
                operationCount++;
                formattedLine = $"🗑️ Deleted: {deleteMatch.Groups[1].Value}";
            }
            else if (createMatch.Success)
            {
                operationCount++;
                formattedLine = $"✨ Created: {createMatch.Groups[1].Value}";
            }
            else if (moveMatch.Success)
            {
                operationCount++;
                formattedLine = $"🔄 Moved: \n  From: {moveMatch.Groups[1].Value}\n  To: {moveMatch.Groups[2].Value}";
            }
            else if (errorMatch.Success)
            {
                formattedLine = $"❌ Error: {errorMatch.Groups[1].Value}";
            }
            else if (warningMatch.Success)
            {
                formattedLine = $"⚠️ Warning: {warningMatch.Groups[1].Value}";
            }
            else if (Regex.IsMatch(line, @"\b(SYNC|START|COMPLETE)\b", RegexOptions.IgnoreCase))
            {
                // For general status messages
                formattedLine = $"ℹ️ {line}";
            }
            else
            {
                // If no specific pattern matches, return the original line
                formattedLine = line;
            }

            return formattedLine;
        }

        private void DisplayLog()
        {
            if (string.IsNullOrWhiteSpace(_rawLog))
            {
                LogTextBlock.Text = "No log data available.";
                UpdateStatusInfo(0);
                return;
            }

            try
            {
                Debug.WriteLine("Attempting to display log content...");

                // Create a formatted log with more readable messages
                var formattedLogLines = new List<string>();
                int operationCount = 0;

                foreach (var line in _rawLog.Split('\n'))
                {
                    // Trim whitespace and skip empty lines
                    var trimmedLine = line.Trim();
                    if (string.IsNullOrEmpty(trimmedLine)) continue;

                    // Parse and format different types of log messages
                    string formattedLine = ParseLogLine(trimmedLine, ref operationCount);

                    if (!string.IsNullOrEmpty(formattedLine))
                    {
                        formattedLogLines.Add(formattedLine);
                    }
                }

                // Join the formatted lines
                LogTextBlock.Text = string.Join("\n\n", formattedLogLines);

                UpdateStatusInfo(operationCount);

                // Ensure proper scrolling
                LogScroller.ScrollToTop();
                LogScroller.ScrollToLeftEnd();

                Debug.WriteLine("Log display completed.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error displaying log: {ex}");
                LogTextBlock.Text = $"Error processing log: {ex.Message}\n\nRaw log:\n{_rawLog}";
                UpdateStatusInfo(0);
            }
        }

        private void UpdateStatusInfo(int operationCount)
        {
            OperationCountText.Text = $"Total operations: {operationCount}";
            TimestampText.Text = $"Sync time: {_timestamp:yyyy-MM-dd HH:mm:ss}";
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(_rawLog);
                MessageBox.Show("Log copied to clipboard.", "Success",
                              MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy log: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Log files (*.log)|*.log|Text files (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = ".log",
                FileName = $"DriveSync_Log_{_timestamp:yyyy-MM-dd_HH-mm-ss}"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(dialog.FileName, _rawLog);
                    MessageBox.Show("Log exported successfully.", "Success",
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to export log: {ex.Message}", "Error",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}