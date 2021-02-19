/*
JpEncoding サンプルプログラム
指定されたファイル名のエンコーディングを判定。

JpEncoding.exe <ファイル名>

*/

using System;
using System.IO;
using Mii;

//エンコードミスを許容しない
JpEncoding.MaxDecodingFailuresCount = 0;

//ファイル全長チェック
JpEncoding.CheckBytes = 0;

if (args.Length > 0 && File.Exists(args[0]))
{
    var enc = JpEncoding.Guess(args[0]);
    Console.WriteLine(enc?.WebName ?? "BINARY");
}
else
{
    Console.WriteLine($"Usage: JpEncoding.exe <ファイル名>");
}