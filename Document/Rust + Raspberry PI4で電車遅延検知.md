# ■前書き  
  
IoT勉強会かねて電車遅延検知を作成したい。  
Google Apps Scriptで作成していましたが、処理起動が1時間単位と大雑把にしか指定できないため、  
自前の管理ガジェットが作ることにしました。  
  
# ■内容  
  
○RasvbrryPi4 Rust環境構築  
○電車遅延アプリ作成  
○定期実行の指定  
  
# ■実装  
  
## ○RasvbrryPi4
### ・rustup インストール  
#### ①下記入力

```  
$ curl https://sh.rustup.rs -sSf | sh  

```    
#### ②1を入力  
```  
1) Proceed with installation (default)  
2) Customize installation  
3) Cancel installation
```  

#### ③完了確認　下記が表示されれればOK  
```  
Rust is installed now. Great!  
```  
  
　  
### ・libssl-dev インストール
  
```  
$ sudo apt install -y libssl-dev  
```  
  
### ・cargo edit インストール
```  
cargo install cargo-edit  
```  
　時間がかかります。  
  
### ・cargo make インストール
  
ソースを落とすためgitをインストール  
  
```  
$ sudo apt install -y git  
```  
  
gitから取得してcargo make インストール  
```  
$ git clone https://github.com/sagiegurari/cargo-make.git  
$ cd cargo-make  
$ cargo install --force cargo-make  
```  
  
これも時間がかかります。  
  
### ・rust を最新版に更新
  
```  
$ rustup update  
```  
  
### ・Hello, worldを試す
  
```  
mkdir ~/hello_world  
$ cd ~/hello_world  
$ vim main.rs  
```  
  
```  
fn main() {  
    // 世界よ、こんにちは  
    println!("Hello, world!");  
}  
```  
  
###  ・コンパイル  
  
新規作成するフォルダに移動  
  
```  
$ cargo new HalloWorld  
$ cd HalloWorld  
$ cd src  
$ rustc main.rs  
$ ./main  
```  
  
コンソールにHello, world!が出ればOK！  
  
## 電車遅延アプリ作成
  
### やっていること
・jsonで公開している電車遅延情報を取得  
・解析して内部で保持している電車路線情報と照合。一致した場合ファイル出力  
・出力ファイルを別途参照して表示（今回は取り扱わない）  
　  
### ・ソース
  
#### [Cargo.toml]  
```  
[package]  
name = "TrainDelayNotification"  
version = "0.1.0"  
authors = ##各自##  
edition = "2018"  
  
# See more keys and their definitions at https://doc.rust-lang.org/cargo/reference/manifest.html  
  
[dependencies]  
serde = { version = "1.0", features = ["derive"] }  
serde_json = "1.0"  
reqwest = { version = "0.9" }  
```  
  
#### [main.rs]  
```  
extern crate serde_json;  
  
use std::fs;  
use std::fs::File;  
use std::io::{Write, BufReader};  
  
use serde::{Deserialize};  
  
#[derive(Deserialize, Debug)]  
struct TrainDelayData {  
    pub name: String,  
    pub company: String,  
    pub lastupdate_gmt: u32,  
    pub source: String  
}  
  
#[derive(Deserialize, Debug, Clone)]  
struct TrainLineData {  
    pub name: String,  
    pub company: String  
}  
  
fn main() -> Result<(), Box<dyn std::error::Error>> {  
  
    //遅延情報の取得  
    let service_uri =  
      "https://tetsudo.rti-giken.jp/free/delay.json";  
    let delays: Vec<TrainDelayData> = reqwest::get(service_uri)?.json()?;  
  
    //チェックする路線を取得  
    let file = File::open("TrainLine.json").unwrap();  
    let reader = BufReader::new(file);  
    let trainLines: Vec<TrainLineData> = serde_json::from_reader(reader).unwrap();  
  
    //遅延チェック  
    let mut viewTrainLines : Vec<TrainLineData> = Vec::<TrainLineData>::new();  
    for delay in delays  
    {  
        for trainLine in &trainLines  
        {  
            if delay.company == trainLine.company  
                && delay.name == trainLine.name  
            {  
                viewTrainLines.push(trainLine.clone());  
            }  
        }  
    }  
  
    //遅延路線の出力  
    let mut output = String::from("");  
    if viewTrainLines.len() != 0 {  
        println!("result={:#?}", viewTrainLines);  
        output += "[\n";  
        for viewTrainLine in &viewTrainLines  
        {  
            output += "{\n";  
            output = format!("{}\"name\":\"{}\", \"company\":\"{}\"\n", output, viewTrainLine.name, viewTrainLine.company);  
            output += "}\n";  
        }  
        output += "]";  
    }  
  
    let mut f = fs::File::create("ViewTrainLine.json").unwrap();  
    f.write_all(output.as_bytes()).unwrap();   
  
    Ok(())  
  }  
```  
### [TrainLine.json]
確認したい路線を登録  
ファイル内容は下記の通り  
```  
[  
    {"name":"大洗鹿島線","company":"鹿島臨海鉄道"}  
]  
```  
  
上記を使用して実行ファイルを作成  
  
  
## ○定期実行の指定
  
crontabコマンドを使用する  
  
下記コマンドを使用して設定ファイルであるcronを編集する  
```  
$ crontab -e  
```  
  
平日　AM7～8時　10分おき　実行を指定  
  
  
# ■最後に
  
rustのライブラリ・説明がまだまだであり、苦労した。  
祝日が設定できないのは今後の改良点です。