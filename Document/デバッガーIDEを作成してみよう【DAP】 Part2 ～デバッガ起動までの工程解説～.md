## ■今回の記事について  
Part1からの続き記事です。  
今回はデバッガーIDEのデバッガ起動までの工程を解説します。  
  
  
## ■修正  
2021/06/13  
調査の結果デバッガ再現のために変更したほうがよろしい点があったため修正。  
デバッガーの起動  
・RequestLunchのStopEntryをfalseに変更  
  
## ■デバッガー起動工程一覧  
１．netcoreDbgのプロセス起動  
２．DAP管理用クラス　DebugAdapterHostを作成  
３．DAP管理用クラス　DebugAdapterHostの起動（Runメソッドの実行）  
４．デバッガーの初期化  
５．デバッガ前の設定完了通知  
６．デバッガーの起動  
７．以下デバッガー操作  
　（サンプルではデバッグ実行）  
８．デバッガーの終了  
  
## ■各工程解説  
### １．netCoreDbgのプロセス起動  
```  
process.StartInfo.FileName = @"■■各netcoredbg.exeのパスに置き換え■■";  
                    process.StartInfo.Arguments = @" --interpreter=vscode";  
                    process.StartInfo.RedirectStandardInput = true;  
                    process.StartInfo.RedirectStandardOutput = true;  
                    process.Start();  
```  
以下の設定は必須です。  
・起動引数：--interpreter=vscode  
　vsCode用のモードで起動  
・RedirectStandardInput =true  
・RedirectStandardOutput =true  
　デバッガーとデバッガー管理用クラスとやり取りするために必要です。  
　インプットアウトプットストリームを使うための設定です。  
### ２．DAP管理用クラス　DebugAdapterHostを作成  
 ```  
var debugAdapterHost = new DebugAdapterHost(process.StandardInput.BaseStream, process.StandardOutput.BaseStream);  
```  
MicroSoftが用意している「DebugAdapterHostBase」を継承したクラスのインスタンス生成します。  
プロセスのインプットとアウトプットのストリームを渡します。  
  
### ３．DAP管理用クラス　DebugAdapterHostの起動（Runメソッドの実行）  
```  
debugAdapterHost.Protocol.Run();  
```  
### ４．デバッガーの初期化  
```  
//初期化  
debugAdapterHost.RequestInitialize();  
debugAdapterHost.WaitForReader();  
```  
```  
public void RequestInitialize()  
{  
    var request = new InitializeRequest();  
    request.Args.ClientID = "vscode";  
    request.Args.ClientName = "Visual Studio Code";  
    request.Args.AdapterID = "coreclr";  
    request.Args.LinesStartAt1 = true;  
    request.Args.ColumnsStartAt1 = true;  
    request.Args.SupportsVariableType = true;  
    request.Args.SupportsVariablePaging = true;  
    request.Args.SupportsRunInTerminalRequest = true;  
    request.Args.Locale = "Jp-jp";  
    Protocol.SendRequestSync(request);  
}  
  
public void WaitForReader()  
{  
    Protocol.WaitForReader(500);  
}  
```  
●デバッガーとのやり取りの基本  
ここよりjsonを送ることで操作していきます。  
リクエストのjsonを送り、レスポンスのjsonを読み取ることで進めていきます。  
  
DebugAdapterHostBaseを継承した場合はjsonの送信受信を担当してくれます。  
実装者としては、リクエスト用のクラスを作成し送信し、レスポンスのクラスを受信して進めていきます。  
  
その後に送っているWaitForReaderはjsonの受信を行います。  
レスポンスの受信はリクエストを送るメソッドで完結するので必要ありませんが、イベントのjson受信をするために実行しています。  
  
●初期化のリクエストについて  
```  
ClientID = "vscode";  
ClientName = "Visual Studio Code";  
AdapterID = "coreclr";  
```  
vsCodeモードで.netCoreを実行するための設定  
```  
Locale = "Jp-jp";  
```  
ロケールの設定  
  
```  
LinesStartAt1 = true;  
ColumnsStartAt1 = true;  
SupportsVariableType = true;  
SupportsVariablePaging = true;  
SupportsRunInTerminalRequest = true;  
```  
その他必要の応じて設定しています  
[Initiaizeリクエストの情報](https://microsoft.github.io/debug-adapter-protocol/specification#Requests_Initialize)  
  
### ５．デバッガ前の設定完了通知  
 ```  
debugAdapterHost.RequstConfigurationDone();  
```  
起動前の設定が完了したことを通知します。  
デバッグを実行するうえでは、このリクエストの前にブレイクポイント設定したりしますが、今回は除外して解説します。  
  
### ６．デバッガーの起動  
 ```  
debugAdapterHost.RequstLunch();  
```  
```  
public void RequstLunch()  
{  
    var request = new LaunchRequest();  
    request.Args.ConfigurationProperties.Add("name", ".NET Core Launch (console) with pipeline");  
    request.Args.ConfigurationProperties.Add("type", "coreclr");  
    request.Args.ConfigurationProperties.Add("preLaunchTask", "build");  
    request.Args.ConfigurationProperties.Add("program", @"■■各自デバッグ対象のパスに置き換え■■");// デバッグ対象のパス 注意:dllを指定すること  
    request.Args.ConfigurationProperties.Add("cwd", "");  
    request.Args.ConfigurationProperties.Add("console", "internalConsole");  
    request.Args.ConfigurationProperties.Add("stopAtEntry", false);  
    request.Args.ConfigurationProperties.Add("internalConsoleOptions", "openOnSessionStart");  
    request.Args.ConfigurationProperties.Add("__sessionId",Guid.NewGuid().ToString());  
    Protocol.SendRequestSync(request);  
}  
```  
この起動の設定はデバッガーや言語によって差異が非常に大きく、共通化されていません。  
プロパティ名と設定値を設定して送ることとなります。  
残念ながらnetCoreDbgの起動設定の情報が非常に少ないため、資料はありません。  
netCoreDbgの単体テストから解析した結果を使用しています。  
  
**特に必要なのはprogramです。**  
デバッグ実行するための実行ファイルへのパスを指定します。  
.net5や.netCore場合dllファイルを指定します。  
  
### ７．以下デバッガー操作  
　（サンプルではデバッグ実行）  
6まででデバッガーの準備は完了です。  
この後に実際のデバッガの実行を行っていきます。  
サンプルのコードでは実行させていきます。  
起動後の解説は次回以降行います。  
  
### ８．デバッガーの終了  
```  
debugAdapterHost.Protocol.Stop();  
```  
  
## ■次回について  
起動後のシーケンス、状態遷移を解説します。  
  
