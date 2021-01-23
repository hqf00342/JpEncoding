using System;
using System.IO;
using System.Text;

/*

JpEncoding - 日本語文字コードを判定

Copyright (c) 2021 Takayuki Nagashima <hqf00342@nifty.com>
Released under the MIT license

https://opensource.org/licenses/mit-license.php


## 改版履歴

  2021-1-20  デコード失敗許容数 MaxDecodingFailuresCount を3に設定。
             .NET Core系で必要な以下のコードを内包
             Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

## 利用方法

C#7.2以上。
(7.2未満で使うときはメソッド引数修飾 in を消すことで可能なはず）  

以下のGuess()メソッドを利用してEncodingを推測する。

  public static Encoding Guess(byte[] bytes)
  public static Encoding Guess(Stream stream)
  public static Encoding Guess(string filename)

デコード失敗はデフォルトで3回まで許容。
厳密判定したい場合は Guess()呼出前に `MaxDecodingFailuresCount=0` を設定。

デフォルトでは先頭64kbyteで判定。変更する場合は `CheckBytes` プロパティに
判定するバイト数を設定。0を設定した場合は全てをチェックする。

(参考)
.NET Core/.NET5では Shift-JISなどのEncodingを生成するために以下のコードが必要。
Guess()メソッド内で フレームワーク判定しているがエラー出るときは一度確認。

    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);


## 判定可能な符号化

| 符号化方式 | 戻り値                      | コードポイント、規格 |
|------------|-----------------------------|----------------------|
| ASCII      | Encoding.ASCII              | CP20127, us-ascii    |
| JIS        | Encoding.GetEncoding(50220) | CP50220, ISO-2022-JP |
| シフトJIS  | Encoding.GetEncoding(932)   | CP932, shift_jis     |
| EUC        | Encoding.GetEncoding(51932) | CP51932, euc-jp      |
| UTF-8      | Encoding.UTF8               | CP65001              |
| UTF-16     | Encoding.Unicode            | CP1200               |


## 参考URL

以下を参考にした。これらはデコード文字数で判定しているが
このコードではデコード失敗数の少なさで判定している。

http://dobon.net/vb/dotnet/string/detectcode.html
http://openlab.ring.gr.jp/Jcode/index-j.html

*/

namespace Mii
{
    public static class JpEncoding
    {
        /// <summary>各デコードで許容するデコード失敗回数.厳密チェックする場合は0に設定</summary>
        public static int MaxDecodingFailuresCount { get; set; } = 3;

        /// <summary>判定に利用するバイト数。</summary>
        public static int CheckBytes { get; set; } = 65536;

        private const int MAX_CHECK_BYTES = int.MaxValue;

        /// <summary>日本語の符号化を推測する(Streamを指定)</summary>
        public static Encoding Guess(Stream stream)
        {
            if (CheckBytes == 0)
            {
                try
                {
                    var streamlen = stream.Length;
                    CheckBytes = streamlen > MAX_CHECK_BYTES ? MAX_CHECK_BYTES : (int)streamlen;
                }
                catch(NotSupportedException)
                {
                    //Length未サポートStreamの場合は仮の値で続行
                    CheckBytes = 65536;
                }
            }
            if (CheckBytes > MAX_CHECK_BYTES)
                CheckBytes = MAX_CHECK_BYTES;

            var buf = new byte[CheckBytes];

            var readlen = stream.Read(buf, 0, CheckBytes);

            if (readlen < CheckBytes)
            {
                Array.Resize<byte>(ref buf, readlen);
                //buf = buf[0..len];
            }

            return Guess(buf);
        }

        /// <summary>日本語の符号化を推測する(ファイル名を指定)</summary>
        public static Encoding Guess(string filename)
        {
            if (CheckBytes == 0)
            {
                var filelen = new FileInfo(filename).Length;
                CheckBytes = filelen > MAX_CHECK_BYTES ? MAX_CHECK_BYTES : (int)filelen;
            }
            if (CheckBytes > MAX_CHECK_BYTES)
                CheckBytes = MAX_CHECK_BYTES;

            using (var stream = File.OpenRead(filename))
            {
                return Guess(stream);
            }
        }

        /// <summary>
        /// 日本語の符号化を推測する
        /// JIS, Shift-JIS, EUC, UTF-8, UTF-16, ASCIIを判別する。
        /// </summary>
        /// <param name="bytes">判定対象のバイト列。内部書換無</param>
        /// <returns>エンコーディング。判定できなかった場合(≒バイナリー)はnull</returns>
        public static Encoding Guess(in byte[] bytes)
        {
#if NETCOREAPP
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
            int len = bytes.Length;
            byte b1, b2, b3, b4;

            //BOMで判定する
            var bomEnc = BOMCheck(bytes);
            if (bomEnc != null) return bomEnc;

            // BOMで判定できなかったので総当たりで判定する。
            // PASS1:ASCII, JIS, UTF16を検出
            bool asciiOnly = true;
            for (int i = 0; i < len; i++)
            {
                //ASCII判定するので末尾までチェックする。
                //なのでforではなく内部で配列範囲チェック。
                b1 = bytes[i];
                b2 = (i > len - 2) ? (byte)0 : bytes[i + 1];
                b3 = (i > len - 3) ? (byte)0 : bytes[i + 2];

                //UTF16のASCII文字
                if (b1 == 0x00 && b2 <= 0x7F)
                    return Encoding.Unicode;

                //どの文字コードにも使われない
                //if (b1 <= 0x06 || b1 == 0x7F || b1 == 0xFF)
                //    return null;

                //JISのシフトコードチェック
                if (b1 == 0x1b)
                {
                    if (b2 == 0x24 && b3 == 0x40) return Encoding.GetEncoding(50220);   // KI 漢字IN
                    if (b2 == 0x24 && b3 == 0x42) return Encoding.GetEncoding(50220);   // 新JIS
                    if (b2 == 0x28 && b3 == 0x42) return Encoding.GetEncoding(50220);   // ASCII
                    if (b2 == 0x28 && b3 == 0x4a) return Encoding.GetEncoding(50220);   // KO 漢字OUT
                    if (b2 == 0x28 && b3 == 0x49) return Encoding.GetEncoding(50220);   // 半角カナ
                }
                if (b1 >= 0x80) asciiOnly = false;
            }

            //0x80以上の文字がなかったのでJISかASCII。JISチェックは終わっているのでASCIIと断定
            if (asciiOnly) return Encoding.ASCII;

            //PASS 2. sjisチェック
            int sjis = 0;       //SJISデコード成功文字数
            int sjis_fail = 0;  //SJISデコード失敗数
            for (int i = 0; i < len - 1; i++)
            {
                if (sjis_fail > MaxDecodingFailuresCount)
                    break;

                b1 = bytes[i];
                b2 = bytes[i + 1];

                if (range(b1, 0x00, 0x7f) || range(b1, 0xa1, 0xdf))
                {
                    //1バイト文字(半角英数字、半角カナ)
                    sjis++;
                }
                else if (range2(b1, 0x81, 0x9F, 0xE0, 0xFC))
                {
                    //SJIS 2バイト文字
                    //2バイト目をチェック
                    if (range2(b2, 0x40, 0x7E, 0x80, 0xFC))
                    {
                        //2バイト目もSJISに則っている
                        sjis += 2;
                        i++;
                    }
                    else
                    {
                        //デコード失敗
                        sjis_fail++;
                    }
                }
                else //if (range(b1, 0xf0, 0xff))
                {
                    //sjis1バイト目にはないコード
                    //デコード失敗
                    sjis_fail++;
                }
            }

            //PASS 3. eucチェック
            int euc = 0;        //EUCデコード成功文字数
            int euc_fail = 0;   //EUCデコード失敗数
            for (int i = 0; i < len - 2; i++)
            {
                if (euc_fail > MaxDecodingFailuresCount)
                    break;

                b1 = bytes[i];
                b2 = bytes[i + 1];
                b3 = bytes[i + 2];

                if (range(b1, 0x00, 0x7f))
                {
                    //1バイト文字
                    euc++;
                }
                else if (b1 == 0x8E)
                {
                    //EUC SS2. 半角カナ。2バイト目チェック
                    if (range(b2, 0xA1, 0xDF))
                    {
                        euc += 2;
                        i++;
                    }
                    else
                    {
                        //デコード失敗
                        euc_fail++;
                    }
                }
                else if (range(b1, 0xA1, 0xFE))
                {
                    //EUC 2バイト文字の可能性大
                    if (range(b2, 0xA1, 0xFE))
                    {
                        euc += 2;
                        i++;
                    }
                    else
                    {
                        //デコード失敗
                        euc_fail++;
                    }
                }
                else if (b1 == 0x8F)
                {
                    //EUC SS3. 3バイト文字 JIS補助漢字相当
                    if (range(b2, 0xA1, 0xFE) && range(b3, 0xA1, 0xFE))
                    {
                        euc += 3;
                        i += 2;
                    }
                    else
                    {
                        //デコード失敗
                        euc_fail++;
                    }
                }
                else //if (!range(b1, 0xa1, 0xfe) && !range(b1, 0x8e, 0x8f))
                {
                    //EUC1バイト目にはありえないコード
                    //デコード失敗
                    euc_fail++;
                }
            }

            //PASS 4. utf-8チェック
            int utf8 = 0;
            int utf8_fail = 0;
            for (int i = 0; i < len - 3; i++)
            {
                if (utf8_fail > MaxDecodingFailuresCount)
                    break;

                b1 = bytes[i];
                b2 = bytes[i + 1];
                b3 = bytes[i + 2];
                b4 = bytes[i + 3];

                if (range(b1, 0x00, 0x7f))
                {
                    //1バイト文字
                    euc++;
                }
                else if (range(b1, 0xC0, 0xDF))
                {
                    //UTF8 2バイト文字
                    if (u82(b2))
                    {
                        utf8 += 2;
                        i++;
                    }
                    else
                    {
                        //デコード失敗
                        utf8_fail++;
                    }
                }
                else if (range(b1, 0xE0, 0xEF))
                {
                    //UTF8 3バイト文字
                    if (u82(b2) && u82(b3))
                    {
                        utf8 += 3;
                        i += 2;
                    }
                    else
                    {
                        //デコード失敗
                        utf8_fail++;
                    }
                }
                else if (range(b1, 0xF0, 0xF7))
                {
                    //UTF8 3バイト文字
                    if (u82(b2) && u82(b3) && u82(b4))
                    {
                        utf8 += 4;
                        i += 3;
                    }
                    else
                    {
                        //デコード失敗
                        utf8_fail++;
                    }
                }
                else
                {
                    //デコード失敗
                    utf8_fail++;
                }
            }

            //判定：デコード失敗数で判定
            //if (sjis_fail == 0) return Encoding.GetEncoding(932);    // SJIS
            //if (euc_fail == 0) return Encoding.GetEncoding(51932);   // EUC
            //if (utf8_fail == 0) return Encoding.UTF8;                // utf8
            //return null;
            // 本来は上のコードでOKだが、数回失敗してもバイナリーになってしまうので
            // MAX_FAIL_COUNTを導入する。

            //判定：すべてNGの場合はバイナリー
            if (sjis_fail == MaxDecodingFailuresCount && euc_fail == MaxDecodingFailuresCount && utf8_fail == MaxDecodingFailuresCount)
                return null;

            //判定：デコード失敗数で判定
            if (sjis_fail < euc_fail && sjis_fail < utf8_fail) return Encoding.GetEncoding(932);    // SJIS
            if (euc_fail < sjis_fail && euc_fail < utf8_fail) return Encoding.GetEncoding(51932);   // EUC
            if (utf8_fail < sjis_fail && utf8_fail < euc_fail) return Encoding.UTF8;                // utf8

            //判定：正答数で判定
            if (euc > sjis && euc > utf8) return Encoding.GetEncoding(51932);   // EUC
            if (sjis > euc && sjis > utf8) return Encoding.GetEncoding(932);    // SJIS
            if (utf8 > euc && utf8 > sjis) return Encoding.UTF8;                // utf8

            //わからなかった.
            return null;

            /// <summary>範囲チェック。 from <= target <= to </summary>
            bool range(byte target, byte from, byte to) => target >= from && target <= to;

            /// <summary>範囲チェック2。 (from1 <= target <= to1) or ((from2 <= target <= to2) </summary>
            bool range2(byte target, byte from1, byte to1, byte from2, byte to2) => range(target, from1, to1) || range(target, from2, to2);

            /// <summary>UTF-8 2バイト目文字チェック</summary>
            bool u82(byte target) => range(target, 0x80, 0xBF);
        }

        /// <summary>
        /// BOM(Byte order mark)からエンコーディングを推測
        /// </summary>
        /// <param name="bom">バイト列。4バイト必要</param>
        /// <returns>エンコーディング。判定できなかった場合はnull</returns>
        private static Encoding BOMCheck(in byte[] bom)
        {
            if (bom.Length < 4)
                return null;

            if (bom[0] == 0xef && bom[1] == 0xbb && bom[2] == 0xbf) return Encoding.UTF8;   // UTF-8
            if (bom[0] == 0xff && bom[1] == 0xfe) return Encoding.Unicode;                  // UTF-16LE
            if (bom[0] == 0xfe && bom[1] == 0xff) return Encoding.BigEndianUnicode;         // UTF-16BE
            if (bom[0] == 0 && bom[1] == 0 && bom[2] == 0xfe && bom[3] == 0xff) return Encoding.UTF32;
            return null;
        }
    }
}