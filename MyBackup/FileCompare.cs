//using System;
//using System.Buffers;
//using System.IO;
//using System.Runtime.Intrinsics;
//using System.Runtime.Intrinsics.X86;

//namespace FileCompare;

//public static class FastFileCompare {
//  public static bool AreFilesEqual(FileInfo fileInfo1, FileInfo fileInfo2, int bufferSize = 4096 * 32) {
//    if (fileInfo1.Exists == false) {
//      throw new FileNotFoundException(nameof(fileInfo1), fileInfo1.FullName);
//    }

//    if (fileInfo2.Exists == false) {
//      throw new FileNotFoundException(nameof(fileInfo2), fileInfo2.FullName);
//    }

//    if (fileInfo1.Length != fileInfo2.Length) {
//      return false;
//    }

//    if (string.Equals(fileInfo1.FullName, fileInfo2.FullName, StringComparison.OrdinalIgnoreCase)) {
//      return true;
//    }

//    using FileStream fileStream01 = fileInfo1.OpenRead();
//    using FileStream fileStream02 = fileInfo2.OpenRead();
//    ArrayPool<byte> sharedArrayPool = ArrayPool<byte>.Shared;
//    byte[] buffer1 = sharedArrayPool.Rent(bufferSize);
//    byte[] buffer2 = sharedArrayPool.Rent(bufferSize);
//    Array.Fill<byte>(buffer1, 0);
//    Array.Fill<byte>(buffer2, 0);
//    try {
//      while (true) {
//        int len1 = 0;
//        for (int read;
//             len1 < buffer1.Length &&
//             (read = fileStream01.Read(buffer1, len1, buffer1.Length - len1)) != 0;
//             len1 += read) {
//        }

//        int len2 = 0;
//        for (int read;
//             len2 < buffer1.Length &&
//             (read = fileStream02.Read(buffer2, len2, buffer2.Length - len2)) != 0;
//             len2 += read) {
//        }

//        if (len1 != len2) {
//          return false;
//        }

//        if (len1 == 0) {
//          return true;
//        }

//        unsafe {
//          fixed (byte* pb1 = buffer1) {
//            fixed (byte* pb2 = buffer2) {
//              int vectorSize = Vector256<byte>.Count;
//              for (int processed = 0; processed < len1; processed += vectorSize) {
//                Vector256<byte> result = Avx2.CompareEqual(Avx.LoadVector256(pb1 + processed), Avx.LoadVector256(pb2 + processed));
//                if (Avx2.MoveMask(result) != -1) {
//                  return false;
//                }
//              }
//            }
//          }
//        }
//      }
//    } finally {
//      sharedArrayPool.Return(buffer1);
//      sharedArrayPool.Return(buffer2);
//    }
//  }
//}