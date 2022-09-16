/**************************************************************************************

MyBackup.MainWindow
===================

User Interface to control backup operation

Written in 2022 by Jürgpeter Huber, Singapore 
Contact: https://github.com/PeterHuberSg/MyBackup

To the extent possible under law, the author(s) have dedicated all copyright and 
related and neighboring rights to this software to the public domain worldwide under
the Creative Commons 0 license (details see LICENSE.txt file, see also
<http://creativecommons.org/publicdomain/zero/1.0/>). 

This software is distributed without any warranty. 
**************************************************************************************/


using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Collections.Generic;


namespace MyBackup {


  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow: Window {

    #region Constructor
    //      -----------

    readonly FileInfo setupDataFile;
    const string setupDataDelimiter = "<-=#=->";


    public MainWindow() {
      InitializeComponent();

      var setupDataDir = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyBackup"));
      if (!setupDataDir.Exists) setupDataDir.Create();

      setupDataFile = new FileInfo(Path.Combine(setupDataDir.FullName, "MyBackupSetup.txt"));
      SetupPathTextBox.Text = setupDataFile.FullName;

      readSetupDataFile();
      updateBackupDriveStats();

      HelpButton.Click += HelpButton_Click;
      BackupPathTextBox.LostFocus += BackupPathTextBox_LostFocus;
      ExecuteButton.Click += ExecuteButton_Click;
      PurgeButton.Click += PurgeButton_Click;
      Closing += MainWindow_Closing;
      //Closed += MainWindow_Closed;
    }
    #endregion


    #region Event Handlers
    //      --------------

    private void HelpButton_Click(object sender, RoutedEventArgs e) {
      var helpWindow = new HelpWindow{Owner=this};
      helpWindow.ShowDialog();
    }


    private void BackupPathTextBox_LostFocus(object sender, RoutedEventArgs e) {
      updateBackupDriveStats();
    }


    private void updateBackupDriveStats() {
      if (BackupPathTextBox.Text.Length==0) {
        DriveStatsTextBlock.Text = "";
        return;
      }
      var backupPathDirectoryInfo = new DirectoryInfo(BackupPathTextBox.Text);
      var driveInfos = DriveInfo.GetDrives();
      var backupRoot = backupPathDirectoryInfo.Root.Name.ToUpperInvariant();
      var backupDriveInfo = driveInfos.Where(di => di.Name.ToUpperInvariant()==backupRoot).FirstOrDefault();
      if (backupDriveInfo is null) {
        DriveStatsTextBlock.Text = $"{backupPathDirectoryInfo.Root.Name} not found";

      } else {
        if (10*backupDriveInfo.AvailableFreeSpace<backupDriveInfo.TotalSize) {
          var response = MessageBox.Show($"Backup drive {backupDriveInfo.Name} has only " +
            $"{BackupStats.ByteCountToString(backupDriveInfo.AvailableFreeSpace)} space left, which is less than " +
            $"10% of the drive space {BackupStats.ByteCountToString(backupDriveInfo.TotalSize)}." + Environment.NewLine + Environment.NewLine +
            "Press Yes to continue, No the cancel the backup.", "Not enough drive space", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
          if (response==MessageBoxResult.No) {
            ExecuteButton.IsChecked = false;
            return;
          }
        }
        var free = BackupStats.ByteCountToString(backupDriveInfo.AvailableFreeSpace);
        var freePercent = 100 * backupDriveInfo.AvailableFreeSpace / backupDriveInfo.TotalSize;
        DriveStatsTextBlock.Text = $"{backupDriveInfo.Name} free {free} {freePercent}%";
      }
    }


    DirectoryInfo[]? copyDirectoryInfos;
    DirectoryInfo[]? updateDirectoryInfos;
    DirectoryInfo? backupPathDirectoryInfo;
    bool isStopBackupThread;
    Thread? backupThread;


    private void ExecuteButton_Click(object sender, RoutedEventArgs e) {
      if (ExecuteButton.IsChecked!.Value) {
        //start backup

        //check if source directories exist
        StringBuilder errorStringBuilder = new();
        copyDirectoryInfos = ensureSourceDirectoriesExist(errorStringBuilder, SourceDirectoriesCopyTextBox);
        updateDirectoryInfos = ensureSourceDirectoriesExist(errorStringBuilder, SourceDirectoriesUpdateTextBox);
        if (errorStringBuilder.Length>0) {
          MessageBox.Show(errorStringBuilder.ToString(), "Cannot find directory", MessageBoxButton.OK, MessageBoxImage.Error);
          ExecuteButton.IsChecked = false;
          return;
        }

        //check if backup directory exists
        backupPathDirectoryInfo = createDirectoryInfo(BackupPathTextBox.Text);
        if (!backupPathDirectoryInfo.Exists) {
          MessageBox.Show($"Cannot find backup path '{BackupPathTextBox.Text}'.");
          ExecuteButton.IsChecked = false;
          return;
        }

        writeSetupDataFile();

        //check if backup drive is less than 90% full
        var driveInfos = DriveInfo.GetDrives();
        var backupRoot = backupPathDirectoryInfo.Root.Name.ToUpperInvariant();
        var backupDriveInfo = driveInfos.Where(di => di.Name.ToUpperInvariant()==backupRoot).FirstOrDefault();
        if (backupDriveInfo is null) {
          var drivesList = "";
          foreach (var drive in driveInfos) {
            drivesList += drive.Name + ", ";
          }
          if (drivesList.Length>2) drivesList = drivesList[..^2];
          throw new IOException($"Free drive space check: Cannot find backup drive {backupPathDirectoryInfo.Root.Name} in {drivesList}.");

        } else {
          if (10*backupDriveInfo.AvailableFreeSpace<backupDriveInfo.TotalSize) {
            var response = MessageBox.Show($"Backup drive {backupDriveInfo.Name} has only " +
              $"{BackupStats.ByteCountToString(backupDriveInfo.AvailableFreeSpace)} space left, which is less than " +
              $"10% of the drive space {BackupStats.ByteCountToString(backupDriveInfo.TotalSize)}." + Environment.NewLine + Environment.NewLine +
              "Press Yes to continue, No the cancel the backup.", "Not enough drive space", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (response==MessageBoxResult.No) {
              ExecuteButton.IsChecked = false;
              return;
            }
          }
        }

        ExecuteButton.Content = "_Stop";
        isStopBackupThread = false;
        backupThread = new Thread(doBackupThread) {
          Name = "Backup"
        };
        backupThread.Start();

      } else {
        //stop backup
        isStopBackupThread = true;
        ExecuteButton.Content = "_Execute";
      }
    }


    private void PurgeButton_Click(object sender, RoutedEventArgs e) {
      var backupDirectory = new DirectoryInfo(BackupPathTextBox.Text);
      var backupFiles = new List<(DateTime Date, DirectoryInfo Dir)>();
      foreach (var directory in backupDirectory.GetDirectories()) {
        if (directory.Name.Length==10 && directory.Name[4]=='_' && directory.Name[7]=='_') {
          backupFiles.Add((new DateTime(int.Parse(directory.Name[..4]), int.Parse(directory.Name[5..7]), int.Parse(directory.Name[8..])), directory));
        }
      }
      backupFiles = backupFiles.OrderBy(bf => bf.Date).ToList();
      var deleteFiles = new List<(DateTime Date, DirectoryInfo Dir)>();
      var sb = new StringBuilder();
      var dirCount = 0;
      foreach (var backupFile in backupFiles) {
        if (dirCount%2 == 1) {
          deleteFiles.Add(backupFile);
          sb.AppendLine(backupFile.Dir.FullName);
        }
        dirCount++;
      }
      if (deleteFiles.Count==0) {
        MessageBox.Show($"No directory found in {BackupPathTextBox.Text} that can be purged." + Environment.NewLine +
          "Every second backup directory can only get deleted if their name looks like 9999_99_99.", "Nothing to purge", 
          MessageBoxButton.OK, MessageBoxImage.Exclamation);
        return;
      }
      var response = MessageBox.Show($"Do you want to delete every second directory in {BackupPathTextBox.Text} ?" + Environment.NewLine +
        sb.ToString(), 
        "Purge Backup Directories", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
      if (response==MessageBoxResult.Yes) {
        BackupLogViewer.WriteLine("Purge: delete some of the backup directories");
        foreach ((DateTime Date, DirectoryInfo Dir) in deleteFiles) {
          try {
            Dir.Delete(recursive: true);
            BackupLogViewer.WriteLine($"deleted {Dir.FullName}.");

          } catch (Exception ex) {
            logException("Exception occured. Cannot delete old backup directory" + Dir.FullName +
              ". Try to delete it manually.", ex);
          }
        }
        BackupLogViewer.WriteLine("Purge completed.");
        BackupLogViewer.WriteLine();
      }
    }


    void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e) {
      isStopBackupThread = true;
    }
    #endregion


    #region Methods
    //      -------

    private void writeSetupDataFile() {
      if (setupDataFile.Exists) setupDataFile.Delete();
      
      var sb = new StringBuilder();
      sb.AppendLine(SourceDirectoriesCopyTextBox.Text);
      sb.AppendLine(setupDataDelimiter);
      sb.AppendLine(SourceDirectoriesUpdateTextBox.Text);
      sb.AppendLine(setupDataDelimiter);
      sb.AppendLine(BackupPathTextBox.Text);
      File.WriteAllText(setupDataFile.FullName, sb.ToString());
    }


    private void readSetupDataFile() {
      if (!setupDataFile.Exists) return;
      
      var setupData = File.ReadAllText(setupDataFile.FullName);
      var setupDataBlocks = setupData.Split(setupDataDelimiter, StringSplitOptions.RemoveEmptyEntries);

      update(SourceDirectoriesCopyTextBox, setupDataBlocks[0]);
      update(SourceDirectoriesUpdateTextBox, setupDataBlocks[1]);
      update(BackupPathTextBox, setupDataBlocks[2]);
    }


    private static void update(TextBox textBox, string text) {
      var startIndex = text.StartsWith(Environment.NewLine) ? Environment.NewLine.Length : 0;
      var endIndex = text.EndsWith(Environment.NewLine) ? text.Length - Environment.NewLine.Length : text.Length;
      textBox.Text =endIndex<=startIndex ? "" : text[startIndex..endIndex];
    }


    private static DirectoryInfo[]? ensureSourceDirectoriesExist(
      StringBuilder errorStringBuilder, 
      TextBox sourceDirectoriesTextBox) 
    {
      if (string.IsNullOrEmpty(sourceDirectoriesTextBox.Text)) return null;

      var sourceDirectoriesTextBoxText = sourceDirectoriesTextBox.Text;
      string[] sourcePaths = sourceDirectoriesTextBoxText.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
      var directoryInfos = new DirectoryInfo[sourcePaths.Length];
      for (int sourcePathsIndex = 0; sourcePathsIndex<sourcePaths.Length; sourcePathsIndex++) {
        var sourcePathDirectory = createDirectoryInfo(sourcePaths[sourcePathsIndex]);
        if (!sourcePathDirectory.Exists) {
          errorStringBuilder.Append(sourcePathDirectory.FullName + Environment.NewLine);
        } else {
          directoryInfos[sourcePathsIndex] = sourcePathDirectory;
        }
      }

      return directoryInfos;
    }


    private static DirectoryInfo createDirectoryInfo(string directoryName) {
      if (!directoryName.EndsWith("\\")) {
        directoryName += '\\';
      }
      return new DirectoryInfo(directoryName);
    }


    private bool makeSureDirectoryExists(DirectoryInfo backupDirectory) {
      try {
        if (!backupDirectory.Exists) {
          backupDirectory.Create();
        }
        return true;
      } catch (Exception ex) {
        logException("Exception occured. Cannot create directory " + backupDirectory.FullName, ex);
        return false;
      }
    }


    private void logException(string message, Exception ex) {
      BackupLogViewer.WriteLine();
      BackupLogViewer.WriteLine(message, StringStyleEnum.errorHeader);
      BackupLogViewer.WriteLine(ex.Message, StringStyleEnum.errorText);
    }
    #endregion


    #region Backup Operations
    //      -----------------

    private void doBackupThread() {
      var totalBackupStats = new BackupStats();
      BackupStats subBackupStats;

      if (copyDirectoryInfos is not null) {

        //copy complete directories
        //-------------------------

        //make sure that backup directory for today's date exists and empty it
        var dateDirectoryName = DateTime.Now.ToString("yyyy_MM_dd");
        var datedBackupDirectory = new DirectoryInfo(backupPathDirectoryInfo!.FullName + dateDirectoryName + '\\');
        if (datedBackupDirectory.Exists) {
          BackupLogViewer.WriteLine("Delete old backup directory: " + datedBackupDirectory.FullName);
          try {
            datedBackupDirectory.Delete(recursive: true);

          } catch (Exception ex) {
            logException("Exception occured. Cannot delete old backup directory" + datedBackupDirectory.FullName +
              ". Try to delete it manually.", ex);
            if (ex is UnauthorizedAccessException) {
              BackupLogViewer.WriteLine("Investigate security rights with File Explorer.", StringStyleEnum.errorText);
            }
            return; //stop thread execution
          }
        }

        BackupLogViewer.WriteLine("Create new backup directory: " + datedBackupDirectory.FullName);
        try {
          datedBackupDirectory.Create();

        } catch (Exception ex) {
          logException("Exception occured. Cannot create empty directory " + datedBackupDirectory.FullName, ex);
          return; //stop thread execution
        }

        subBackupStats = new BackupStats();
        BackupLogViewer.WriteLine("Start backup of completely copied directories");
        foreach (var copyDirectoryInfo in copyDirectoryInfos!) {
          BackupLogViewer.WriteLine($"Backup {copyDirectoryInfo.FullName}");
          var backupStats = new BackupStats();
          if (copyDirectoryInfo.Name=="repos") {
            backupVSSolutions(
              copyDirectoryInfo,
              datedBackupDirectory.FullName,
              backupStats);

          } else {
            backupCopyDirectories(
              copyDirectoryInfo,
              datedBackupDirectory.FullName,
              level: 0,
              backupStats);
          }

          BackupLogViewer.WriteLine(backupStats.ToShortString() +
            $" Backup {copyDirectoryInfo.FullName} {(isStopBackupThread ? "stopped" : "completed")}", StringStyleEnum.stats);
          subBackupStats.Add(backupStats);

          if (isStopBackupThread) break;
        }

        BackupLogViewer.WriteLine(subBackupStats.ToShortString() +
          $" Copying directories {(isStopBackupThread ? "stopped" : "completed")}: ", StringStyleEnum.stats);
        totalBackupStats.Add(subBackupStats);
      }

      if (updateDirectoryInfos is not null) {

        //update directories
        //------------------

        subBackupStats = new BackupStats();
        BackupLogViewer.WriteLine("Start backup of updated directories");
        foreach (var updateDirectoryInfo in updateDirectoryInfos!) {
          BackupLogViewer.WriteLine($"Backup {updateDirectoryInfo.FullName}");
          var backupStats = new BackupStats();
          backupUpdateDirectories(
            updateDirectoryInfo,
            backupPathDirectoryInfo!.FullName,
            level: 0,
            backupStats);

          BackupLogViewer.WriteLine(backupStats.ToShortString() +
            $" Backup {updateDirectoryInfo.FullName} {(isStopBackupThread ? "stopped" : "completed")}", StringStyleEnum.stats);
          subBackupStats.Add(backupStats);

          if (isStopBackupThread) break;
        }

        BackupLogViewer.WriteLine(subBackupStats.ToShortString() +
          $" Updating directories {(isStopBackupThread ? "stopped" : "completed")}: ", StringStyleEnum.stats);
        totalBackupStats.Add(subBackupStats);
      }

      BackupLogViewer.Write(new StyledString[]{
          new StyledString($"Backup {(isStopBackupThread ? "stopped" : "completed")}", StringStyleEnum.header1),
          new StyledString(LineHandlingEnum.endOfLine),
          new StyledString(totalBackupStats.ToShortString() +
                $" Total backedup", StringStyleEnum.stats)});

      this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
       new Action(reportBackupCompleted));
    }


    private void reportBackupCompleted() {
      ExecuteButton.IsChecked = false;
      ExecuteButton.Content = "_Execute";
      updateBackupDriveStats();
    }


    /// <summary>
    /// backup directories which get completely copied each time
    /// </summary>
    private void backupCopyDirectories(
      DirectoryInfo sourceDirectory,
      string backupPath,
      int level,
      BackupStats backupStats) 
    {
      //create directory at backup location
      backupPath = System.IO.Path.Combine(backupPath, sourceDirectory.Name);
      try {
        Directory.CreateDirectory(backupPath);
        backupStats.DirsCount++;

      } catch (Exception ex) {
        logException($"Exception occured. Cannot create directory '{backupPath}'.", ex);
        backupStats.ErrorsCount++;
        return;
      }

      var localBackupStats = new BackupStats();

      //copy all files from source to backup
      foreach (var file in sourceDirectory.GetFiles()) {
        if (isStopBackupThread) break;

        try {
          BackupLogViewer.WriteTempLine($"{file.FullName}: {BackupStats.ByteCountToString(file.Length)}");
          copyFile(file, backupPath);

          localBackupStats.FilesCount++;
          localBackupStats.TotalFilesCount++;
          localBackupStats.BytesCount += file.Length;
          localBackupStats.TotalBytesCount += file.Length;

        } catch (Exception ex) {
          logException($"Exception occured. Cannot copy file '{file.FullName}' to '{System.IO.Path.Combine(backupPath, file.Name)}'.", ex);
          localBackupStats.ErrorsCount++;
        }
      }

      //backup all child directories
      level++;
      foreach (var sourceChildDirectoryInfo in sourceDirectory.GetDirectories()) {
        if (isStopBackupThread) break;

        backupCopyDirectories(
          sourceChildDirectoryInfo,
          backupPath,
          level,
          localBackupStats);
      }

      if (level==2) {//detects directories with level 1
        BackupLogViewer.WriteLine(localBackupStats.ToShortString() +
          $"{sourceDirectory.FullName}", StringStyleEnum.stats);
      }
      backupStats.Add(localBackupStats);
    }


    /// <summary>
    /// backup VS solutions
    /// </summary>
    private void backupVSSolutions(
      DirectoryInfo sourceDirectory,
      string backupPath,
      BackupStats backupStats) 
    {
      //create directory at backup location
      backupPath = System.IO.Path.Combine(backupPath, sourceDirectory.Name);
      try {
        Directory.CreateDirectory(backupPath);
        backupStats.DirsCount++;

      } catch (Exception ex) {
        logException($"Exception occured. Cannot create directory '{backupPath}'.", ex);
        backupStats.ErrorsCount++;
        return;
      }

      var localBackupStats = new BackupStats();

      //backup all child directories
      foreach (var sourceChildDirectoryInfo in sourceDirectory.GetDirectories()) {
        var solutionBackupStats = new BackupStats();
        if (isStopBackupThread) break;
        backupSWDirectories(
          sourceChildDirectoryInfo,
          backupPath,
          solutionBackupStats);
        BackupLogViewer.WriteLine(solutionBackupStats.ToShortString() +
          $"{sourceChildDirectoryInfo.FullName}", StringStyleEnum.stats);
        localBackupStats.Add(solutionBackupStats);
      }

      backupStats.Add(localBackupStats);
    }


    /// <summary>
    /// backup directories belonging to VS solutions
    /// </summary>
    private void backupSWDirectories(
      DirectoryInfo sourceDirectory,
      string backupPath,
      BackupStats backupStats) 
    {
      //create directory at backup location
      backupPath = System.IO.Path.Combine(backupPath, sourceDirectory.Name);
      try {
        Directory.CreateDirectory(backupPath);
        backupStats.DirsCount++;

      } catch (Exception ex) {
        logException($"Exception occured. Cannot create directory '{backupPath}'.", ex);
        backupStats.ErrorsCount++;
        return;
      }

      //copy all files from source to backup
      foreach (var file in sourceDirectory.GetFiles()) {
        if (isStopBackupThread) break;

        try {
          BackupLogViewer.WriteTempLine($"{file.FullName}: {BackupStats.ByteCountToString(file.Length)}");
          copyFile(file, backupPath);
          backupStats.FilesCount++;
          backupStats.TotalFilesCount++;
          backupStats.BytesCount += file.Length;
          backupStats.TotalBytesCount += file.Length;

        } catch (Exception ex) {
          logException($"Exception occured. Cannot copy file '{file.FullName}' to '{System.IO.Path.Combine(backupPath, file.Name)}'.", ex);
          backupStats.ErrorsCount++;
        }
      }

      //backup all child directories
      foreach (var sourceChildDirectoryInfo in sourceDirectory.GetDirectories()) {
        if (isStopBackupThread) break;

        if (sourceChildDirectoryInfo.Name is ".git" or ".vs" or "bin" or "obj") continue; //don't backup these directories

        backupSWDirectories(
          sourceChildDirectoryInfo,
          backupPath,
          backupStats);
      }
    }


    /// <summary>
    /// backup directories which get completely copied each time
    /// </summary>
    private void backupUpdateDirectories(
      DirectoryInfo sourceDirectory,
      string backupPath,
      int level,
      BackupStats backupStats) 
    {
      //create directory at backup location
      backupPath = System.IO.Path.Combine(backupPath, sourceDirectory.Name);
      if (!new DirectoryInfo(backupPath).Exists) {
        try {
          Directory.CreateDirectory(backupPath);

        } catch (Exception ex) {
          logException($"Exception occured. Cannot create directory '{backupPath}'.", ex);
          backupStats.ErrorsCount++;
          return;
        }
      }
      backupStats.DirsCount++;//count directory even if no file in the directory needed updating

      var localBackupStats = new BackupStats();

      //update all files from source to backup
      foreach (var file in sourceDirectory.GetFiles()) {
        if (isStopBackupThread) break;

        var destination = System.IO.Path.Combine(backupPath, file.Name);
        var destinationFileInfo = new FileInfo(destination);
        if (!destinationFileInfo.Exists || file.Length!=destinationFileInfo.Length || file.LastWriteTime!=destinationFileInfo.LastWriteTime) {
          try {
            BackupLogViewer.WriteTempLine($"{file.FullName}: {BackupStats.ByteCountToString(file.Length)}");
            file.CopyTo(destination, overwrite: true);
            if (file.IsReadOnly) {
              destinationFileInfo.Refresh();
              destinationFileInfo.IsReadOnly = false;
            }
            localBackupStats.FilesCount++;
            localBackupStats.TotalFilesCount++;
            localBackupStats.BytesCount += file.Length;
            localBackupStats.TotalBytesCount += file.Length;

          } catch (Exception ex) {
            logException($"Exception occured. Cannot copy file '{file.FullName}' to '{System.IO.Path.Combine(backupPath, file.Name)}'.", ex);
            localBackupStats.ErrorsCount++;
          }
        } else {
          localBackupStats.TotalFilesCount++;
          localBackupStats.TotalBytesCount += file.Length;
        }
      }

      //backup all child directories
      level++;
      foreach (var sourceChildDirectoryInfo in sourceDirectory.GetDirectories()) {
        if (isStopBackupThread) break;

        backupUpdateDirectories(
          sourceChildDirectoryInfo,
          backupPath,
          level,
          localBackupStats);
      }

      if (level==2) {//detects directories with level 1
        BackupLogViewer.WriteLine(localBackupStats.ToShortString() +
          $"{sourceDirectory.FullName}", StringStyleEnum.stats);
      }
      backupStats.Add(localBackupStats);
    }


    private static void copyFile(FileInfo file, string backupPath) {
      var destination = System.IO.Path.Combine(backupPath, file.Name);
      file.CopyTo(destination);
      var destinationFileInfo = new FileInfo(destination);
      if (destinationFileInfo.IsReadOnly) {
        destinationFileInfo.IsReadOnly = false;
      }
    }
    #endregion
  }
}
