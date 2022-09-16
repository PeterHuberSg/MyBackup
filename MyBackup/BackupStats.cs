/**************************************************************************************

MyBackup.BackupStats
====================

Collects some backup operational stas, like when it started, how many files where handled, etc.

Written in 2022 by Jürgpeter Huber, Singapore 
Contact: https://github.com/PeterHuberSg/MyBackup

To the extent possible under law, the author(s) have dedicated all copyright and 
related and neighboring rights to this software to the public domain worldwide under
the Creative Commons 0 license (details see LICENSE.txt file, see also
<http://creativecommons.org/publicdomain/zero/1.0/>). 

This software is distributed without any warranty. 
**************************************************************************************/


using System;


namespace MyBackup {


  public class BackupStats {

    #region Properties
    //      ----------

    public DateTime StartTime { get; set; }

    //stats for all files scanned
    public int TotalFilesCount { get; set; }
    public long TotalBytesCount { get; set; }

    //stats for backuped files
    public int DirsCount { get; set; }
    public int FilesCount { get; set; }
    public long BytesCount { get; set; }
    public int ErrorsCount { get; set; }
    #endregion


    #region Constructor
    //      -----------

    public BackupStats() {
      StartTime = DateTime.Now;
    }
    #endregion


    #region Methods
    //      -------

    public void Add(BackupStats detailBackupStats) {
      TotalFilesCount += detailBackupStats.TotalFilesCount;
      TotalBytesCount += detailBackupStats.TotalBytesCount;
      DirsCount += detailBackupStats.DirsCount;
      FilesCount += detailBackupStats.FilesCount;
      BytesCount += detailBackupStats.BytesCount;
      ErrorsCount += detailBackupStats.ErrorsCount;
    }


    public string ToShortString() {
      return
        $"{(DateTime.Now-StartTime):hh\\:mm\\:ss};{DirsCount};{FilesCount};{TotalFilesCount};" + 
        $"{ByteCountToString(BytesCount)};{ByteCountToString(TotalBytesCount)};{ErrorsCount};";   }


    override public string ToString() {
      return
        $"{StartTime: (StartTime):hh\\:mm\\:ss}; DirsCount: {DirsCount}; FilesCount: {FilesCount}/{TotalFilesCount}; " + 
        $"ByteCount: {ByteCountToString(BytesCount)}/{ByteCountToString(TotalBytesCount)}; ErrorsCount: {ErrorsCount};";
    }


    private const long oneKilo = 1024L;
    private const float oneKiloFloat = oneKilo;


    public static string ByteCountToString(long byteCount) {
      if (byteCount<oneKilo) {
        return byteCount.ToString() + " Bytes";
      }
      float byteCountFloat = byteCount / oneKiloFloat;
      byteCount /= oneKilo;
      if (byteCount<oneKilo) {
        return byteCountFloat.ToString("###.###") + " kBytes";
      }
      byteCountFloat = byteCount / oneKiloFloat;
      byteCount /= oneKilo;
      if (byteCount<oneKilo) {
        return byteCountFloat.ToString("###.###") + " MBytes";
      }
      byteCountFloat = byteCount / oneKiloFloat;
      byteCount /= oneKilo;
      if (byteCount<oneKilo) {
        return byteCountFloat.ToString("###.###") + " GBytes";
      }
      byteCountFloat = byteCount / oneKiloFloat;
      byteCount /= oneKilo;
      return byteCountFloat.ToString("###.###") + " TBytes";
    }
    #endregion
  }
}
