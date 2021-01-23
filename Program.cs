using System;
using System.IO;
using Mii;

//オプション：エンコードミスを許容しない。デフォルトは3
JpEncoding.MaxDecodingFailuresCount = 0;

//オプション：ファイル全長をチェックする。デフォルトは64Kbyte
JpEncoding.CheckBytes = 0;

if (args.Length > 0 && File.Exists(args[0]))
{
    var enc = JpEncoding.Guess(args[0]);
    Console.WriteLine(enc?.WebName ?? "BINARY");
}
else
{
    Console.WriteLine($"Usage: JpEncodingTest.exe <ファイル名>");
}