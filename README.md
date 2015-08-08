# nicoproxy

#### English

This is a proxy for caching videos on nicovideo.jp

If you specify this program as proxy, it saves videos that you are going to watch or send saved video to your browser.

This program is released under the MIT License, see LICENSE.txt

To use this proxy, you have to download some libraries:
- http://trotinet.sourceforge.net/ (source code)
- http://www.newtonsoft.com/json (source code or binary, installing on visual studio is easy)

and implement a property:
- TrotiNet.HttpHeaders.Range

and build libraries.

I have reached the limits of the library this program use. so I won't develop this anymore.

#### 日本語

ニコニコの動画をローカルにキャッシュしておくためのプロキシです。

同じ動画を何度も見る場合には通信量削減が見込めます。それ以外には役に立ちません。

ライブラリの限界を感じて途中で投げましたが一応動きます。ただし、3つ以上の動画を同時に読み込ませようとした場合や、同一の動画に複数のページからアクセスした場合には挙動がおかしくなることがあります。

自分からはリクエストを生成しないのでログインは不要です。

使用の際は、インターネットに向けてポートが開いていないことを確認してください。

ビルド方法は上の英語の文章を読んでください。

ライセンスはMITライセンスです。LICENSE.txtをご覧ください。


