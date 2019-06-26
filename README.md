# VRMSpringBone-Optimize

大量のVRMモデルを動かすことを想定して、[dwango/UniVRM](https://github.com/dwango/UniVRM)に含まれる[VRMSpringBone](https://github.com/dwango/UniVRM/blob/master/Assets/VRM/UniVRM/Scripts/SpringBone/VRMSpringBone.cs)を**C# JobSystem** ベースで最適化してみたもの。

- ※ パフォーマンスの計測結果諸々については[master](https://github.com/mao-test-h/VRMSpringBone-Optimize)ブランチに有るReadmeを参照
- ※ 実用性を想定して以下の理由からECS対応は一旦オミット
    - パフォーマンスや動的なモデルの追加/削除に伴う負荷を見た感じだと、JobSystemでも十分に稼げているので無理して使う必要が無かった為
    - 変更点への対応追従(ECS自体がまだPreviewなので)


------------------------------------------------------------------------------------------------

# 各種バージョン

- Unity
    - 2018.3.14f1
- UniVRM
    - UniVRM-0.50_9db7.unitypackage
    - UniVRM.asmdef-0.50_9db7.unitypackage


## 依存パッケージ

各モジュールはPackageManagerから取得できる幾つかのパッケージに依存しているので、利用する際には事前に入れておくこと。

- **VRMSpringBoneOptimize-Jobs**
    - com.unity.burst: 1.0.4
    - com.unity.mathematics: 1.0.1
    - com.unity.collections: 0.0.9-preview.12



------------------------------------------------------------------------------------------------

# 各モジュールについて

## VRMSpringBoneOptimize-Jobs

C# JobSystemベースで実装してみたもの。  
ソースについては"VRMSpringBoneOptimize/Jobs"以下を参照。  

機能としてはJobのScheduleに関する管理方法の違いで以下の2点を実装している。  

- **CentralizedBuffer**
    - 全モデルのJobに関するデータを1箇所で集中管理し、一括でJobのScheduleを行うタイプの物。
    - **メリット**
        - 一括で行うので数が多いほど効率よく処理できるので早い。
    - **デメリット**
        - データを集中管理している都合上、動的なモデルの登録/解除を行った際にJobに渡すデータの作り直しが発生するので負荷が高くなる。
- **DistributedBuffer**
    - Jobに関するデータをモデル毎に独立して持たせた上で、モデル毎にJobのScheduleを行うタイプの物。
    - **メリット**
        - データが分散管理されているので動的にモデルを登録/解除しても負荷が高くない。
    - **デメリット**
        - データ及びScheduleの数がモデル毎に行われるために効率が悪い。

## 導入方法

こちらを有効にするには「ENABLE_JOB_SPRING_BONE」と言うシンボルを定義する必要がある。  
→ Assembly Definition側で設定している

使い方については`CentralizedBuffer`と`DistributedBuffer`共に設定について大きな違いは無いので、前者を取り上げる形で説明していく。

### 事前にモデルに設定する場合

#### 1. モデルに対する設定

- VRMモデルのルートオブジェクト(VRMMetaなどがアタッチされている物)に対し`CentralizedBuffer`をアタッチ。
    - ※この設定は必須ではない。後述の3の手順にあるSchedulerの登録時にGameObjectを渡すことで動的にアタッチ/登録させることも可能。
- VRMモデルに設定されている`VRMSpringBone`と`VRMSpringBoneColliderGroup`を"VRMSpringBoneOptimize/Jobs/Scripts"以下にある同名のScriptに置き換える。
    - こちらは`Centralize`と`Distributed`共通の設定。
    - 置き換え用の拡張を実装してある。メニューにある「VRMSpringBoneOptimize/Replace SpringBone Components - Jobs」を実行することで、Scene中にあるVRMモデルに対し一括で上記2点をJobSystem向けのものに置き換える事が可能。


#### 2. スケジューラーの生成

- 任意のGameObjectに対し`CentralizedJobScheduler`をアタッチ
    - ※画像の例だと管理クラス用に`Scheduler`と言うGameObjectを用意してそちらにアタッチしている。

![job_model_settings2](https://github.com/mao-test-h/VRMSpringBone-Optimize/blob/master/Documents/img/job_model_settings2.png)


#### 3. Jobの登録/解除について

- `CentralizedJobScheduler`に以下の関数を実装しているので、こちらに対し登録/解除対象の`CentralizedBuffer`を渡すこと。
    - 登録 : `CentralizedJobScheduler.AddBuffer(`CentralizedJobScheduler`)`
    - 解除 : `CentralizedJobScheduler.RemoveBuffer(`CentralizedJobScheduler`)`
        - ※オーバーロードでGameObjectも渡せるようにしてある。その場合には渡したObjectに`CentralizedBuffer`がアタッチされていなかったら動的にアタッチしてから登録する仕様となる。
- `CentralizedJobScheduler`の初期化タイミングで良ければこちらのInspectorから設定できる`IsAutoGetBuffer`を有効にすることで、MonoBehaviour.Startのタイミングでシーン中に存在する`CentralizedJobScheduler`を自動で集めて登録することが可能。


### ランタイムロードの場合

#### 1. モデルに対する設定

VRMモデルに設定されている`VRMSpringBone`と`VRMSpringBoneColliderGroup`を"VRMSpringBoneOptimize/Jobs/Scripts"以下にある同名のScriptに置き換える必要があるが、やり方は2つある。

##### ① `ReplaceComponents.ReplaceJobs(GameObject obj)`の利用

上記のstatic methodを用意しているので、こちらにGameObjectを渡すことで一括で置き換えることが可能。

**但し、問題点としてこちらのやり方で差し替える場合には微妙にノードの位置などがずれてしまう事が有るので注意。**

具体的に話すとVRMをロード後に数フレーム経過してからJob版のComponentへ差し替えを行うと、差し替える前の`VRM.VRMSpringBone`のUpdateなどが先に呼ばれてしまってBoneの位置が動いてしまう可能性がある。  

その状態で差し替えると初期位置がずれたままとなるので、結果として正常な挙動と比べると微妙に違ってくる可能性が出てきてしまうので確実に潰すならUpdateが呼ばれる前に差し替えるか以下の②の手順を行う必要がある。

##### ②. `VRM.VRMSpringUtility`の改造

※記載の通り、UniVRMに手を加えることになるのでバージョン管理や変更に伴う副作用などは自己責任。

ロードしたVRMのSpringBoneの設定は上記のクラスで解析/Componentのアタッチや設定などが行われる模様。  
力技にはなってしまうが..シリアライズ可能なフィールドは全て互換性を保つようにしているので、こちらでアタッチされているComponentをJobSystem版のものに変更する事で自動的に差し替えることが可能。  
(シンボル定義時とそうでない時でアタッチするComponentを切り替えられるように変更するのも有りかもしれない。)


#### 2. スケジューラーの生成
#### 3. Jobの登録/解除について

※「事前にモデルに設定する場合」に記載している内容と同じ。



------------------------------------------------------------------------------------------------

# License

- [dwango/UniVRM - LICENSE](https://github.com/dwango/UniVRM/blob/master/LICENSE.txt)


