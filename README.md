# JpEncoding

テキストファイルの日本語エンコーディングを特定する ライブラリ。C#製。  

## 使い方

C#7.2以上。  
`JpEncoding.cs` をプログラムへ組み込み、Guess()メソッドで日本語エンコーディングを推測します。

```
using Mii;

  JpEncoding.Guess(byte[] bytes)
  JpEncoding.Guess(Stream stream)
  JpEncoding.Guess(string filename)
```

戻り値は `Encoding` 。判定できなかった場合はnull.  
サンプルプログラムとして `Program.cs` を参考にしてください。(.NET5)

**戻り値**

| 符号化方式 | 戻り値                      | コードポイント等     |
|------------|-----------------------------|----------------------|
| ASCII      | Encoding.ASCII              | CP20127, us-ascii    |
| JIS        | Encoding.GetEncoding(50220) | CP50220, ISO-2022-JP |
| シフトJIS  | Encoding.GetEncoding(932)   | CP932, shift_jis     |
| EUC        | Encoding.GetEncoding(51932) | CP51932, euc-jp      |
| UTF-8      | Encoding.UTF8               | CP65001              |
| UTF-16     | Encoding.Unicode            | CP1200               |
| 判別不能   | null                        | (バイナリー)         |


また以下のプロパティを設定することで判定の調整ができます。

**MaxDecodingFailuresCount**   
判定の厳密さを設定可能。デフォルト値:3  
厳密に判定したい場合は Guess()を呼び出す前に 0 に設定する。


**CheckBytes**

判定に利用するバイト数。デフォルト値:64kbyte。
0とすると全てをチェックする。


## 符号化判定方法

1. BOMがあればBOMで判定。
2. BOMがなければ1バイトずつ各符号化違反がないかをチェック。
3. 違反数が最も少ない Encoding を戻り値とします。ただし違反許容数 `MaxDecodingFailuresCount` をどれも超えた場合はバイナリーと判定し `null` を返します。

### JIS

7bit文字で構成される符号方式。ASCII範囲(00～7F)は1バイト文字。その他は2バイト。
ASCII範囲外を使う場合はエスケープシーケンス（3バイト）で文字集合を切替える。
このシーケンスが出現するかどうかで判定。

| ESC seq    | 文字集合切替先        |
|------------|-----------------------|
| 1B 28 42   | ASCII                 |
| 1B 28 4A   | JISローマ字           |
| 1B 28 49   | JISカナ（半角カナ）   |
| 1B 24 40   | 旧JIS漢字 (1978)      |
| 1B 24 42   | 新JIS漢字 (1983/1990) |
| 1B 24 44   | JIS補助漢字           |

### Shift-JIS

ASCII範囲(00～7F)と半角カナ(A0～DF)は1バイト文字。
半角カナの存在と2バイト目が広くEUCとの判定が困難なデコード失敗数を比較し少ないもので判定する。

| 1st octet      | 2nd            |  文字の種類        |
|----------------|----------------|--------------------|
| 00～7F         |                | ASCII,制御コード   |
| A0～DF         |                | 半角カナ           |
| 81～9F, E0～EF | 40～7E, 80～FC | 2バイト文字        |

### EUC

１～3バイトで1文字を表現。 以下のルールが出現するかで判定。

| 1st octet | 2nd    | 3rd    | 文字の種類        |
|-----------|--------|--------|-------------------|
| 00～7F    |        |        | ASCII,制御コード  |
| A1～FE    | A1～FE |        | JIS漢字           |
| 8E        | A1～DF |        | JISカナ(半角カナ) |
| 8F        | A1～FE | A1～FE | JIS補助漢字       |

### UTF-8

1～4バイトでUNICODEを表現した符号法。
5～6バイト文字の ISO 10646 規格は使われていないため以下のルールで判定。

| 1st octet | 2nd    | 3rd    |4th     | 文字の種類        |
|-----------|--------|--------|--------|-------------------|
| 00～7F    |        |        |        | ASCII/制御コード  |
| C0～DF    | 80～BF |        |        | 2バイト文字       |
| E0～EF    | 80～BF | 80～BF |        | 3バイト文字       |
| F0～F7    | 80～BF | 80～BF | 80～BF | 4バイト文字       |

### UTF-16

UNICODEスカラー値をそのまま使った符号化。
テキスト中にASCII文字が多く出現すると勝手に想定し
[`00`, `00～7F`] の2バイト列が出現したらUTF-16と判定。

## 参考

以下のC#コードの利用を検討しましたが誤判定が散見されるので上記アルゴリズムで自作しています。  

http://dobon.net/vb/dotnet/string/detectcode.html
http://openlab.ring.gr.jp/Jcode/index-j.html

`MaxDecodingFailuresCount=0` とすることで nkf とほぼ同じ結果を得ています。
(nkf はかなり厳密でShift-JISと CP932 も分けて判定している。)


## ライセンス

MIT

Copyright (c) 2021 Takayuki Nagashima

