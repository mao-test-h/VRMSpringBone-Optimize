# VRMSpringBone-Optimize

大量のVRMモデルを動かすことを想定して、[dwango/UniVRM](https://github.com/dwango/UniVRM)に含まれる[VRMSpringBone](https://github.com/dwango/UniVRM/blob/master/Assets/VRM/UniVRM/Scripts/SpringBone/VRMSpringBone.cs)を**C# JobSystem** or **ECS(EntityComponentSystem)** ベースで最適化してみたもの。

Releasesページにて以下の3点を公開している。  
それぞれの詳細及び使い方については後述。

- **VRMSpringBoneOptimize-JobSystem**
    - C# JobSystemベースでの実装
- **VRMSpringBoneOptimize-Entities**
    - C# JobSystem & ESCベースでの実装
- **VRMSpringBoneOptimize**
    - 上記2点を含んだもの。



------------------------------------------------------------------------------------------------

# 各種バージョン

- Unity
    - 2018.3.5f1で開発/動作確認 
- UniVRM
    - UniVRM-0.49_43af.unitypackage


## 依存パッケージ

各モジュールはPackageManagerから取得できる幾つかのパッケージに依存しているので、利用する際には事前に入れておくこと。

- **VRMSpringBoneOptimize-JobSystem**
    - com.unity.burst": "0.2.4-preview.41
    - com.unity.collections": "0.0.9-preview.11
    - com.unity.jobs": "0.0.7-preview.6
    - com.unity.mathematics": "0.0.12-preview.19
- **VRMSpringBoneOptimize-Entities**
    - "com.unity.entities": "0.0.12-preview.23"
    - "com.unity.test-framework.performance": "0.1.50-preview"
        - ※「Unity Performance Testing Extension」はentities 0.0.12-preview.23が依存しているために必要となる。
            - こちらについてはPackageManagerから取得できないので注意。[取得方法はこちらを参照](https://docs.unity3d.com/Packages/com.unity.test-framework.performance@0.1/manual/index.html)
    - ※以下のPackageにも依存しているがcom.unity.entitiesに付属しているので別途取得不要
        - com.unity.burst": "0.2.4-preview.41
        - com.unity.collections": "0.0.9-preview.11
        - com.unity.jobs": "0.0.7-preview.6
        - com.unity.mathematics": "0.0.12-preview.19

表記しているバージョンについては実装時のものとなるが、`com.unity.entities`以外については恐らくは最新の物が出たタイミングで更新してしまっても問題ないかと思われる。


------------------------------------------------------------------------------------------------

# 各モジュールについて


## VRMSpringBoneOptimize-JobSystem

C# JobSystemベースで実装してみたもの。  
ソースについては"VRMSpringBoneOptimize/JobSystem"以下を参照。  

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

### 導入方法

こちらを有効にするには「ENABLE_JOB_SPRING_BONE」と言うシンボルを定義する必要がある。  
→ Assembly Definition側で設定している

使い方については`CentralizedBuffer`と`DistributedBuffer`共に設定について大きな違いは無いので、前者を取り上げる形で説明していく。

#### 1. モデルに対する設定

- VRMモデルの階層以下にあるSpringBoneが管理されているオブジェクト(スクリーンショットの例で言えば`secondary`)に`CentralizedBuffer`をアタッチ
- VRMモデルに設定されている`VRMSpringBone`と`VRMSpringBoneColliderGroup`を"VRMSpringBoneOptimize/JobSystem/Scripts"以下にある同名のScriptに置き換える。
    - TODO: 後で一括で置き換えてくれる拡張を入れる

![job_model_settings1](https://github.com/mao-test-h/VRMSpringBone-Optimize/blob/feature/dev_update/Documents/img/job_model_settings1.png)


#### 2. スケジューラーの生成

- 任意のGameObjectに対し`CentralizedJobScheduler`をアタッチ
    - ※画像の例だと管理クラス用に`Scheduler`と言うGameObjectを用意してそちらにアタッチしている。

![job_model_settings2](https://github.com/mao-test-h/VRMSpringBone-Optimize/blob/feature/dev_update/Documents/img/job_model_settings2.png)


### 3. Jobの登録/解除について

- `CentralizedJobScheduler`に以下の関数を実装しているので、こちらに対し登録/解除対象の`CentralizedBuffer`を渡すこと。
    - 登録 : `CentralizedJobScheduler.AddBuffer(`CentralizedJobScheduler`)`
    - 解除 : `CentralizedJobScheduler.RemoveBuffer(`CentralizedJobScheduler`)`
- ※`CentralizedJobScheduler`の初期化タイミングで良ければこちらのInspectorから設定できる`IsAutoGetBuffer`を有効にすることで、MonoBehaviour.Startのタイミングでシーン中に存在する`CentralizedJobScheduler`を自動で集めて登録することが可能。




## VRMSpringBoneOptimize-Entities

C# JobSystem & ESCベースで実装してみたもの。  
(内部実装としてはHybridECSをベースとしつつ計算周りをJob化しているイメージ)

なお、ECS側の実装に関してはTransformの更新負荷軽減の為に敢えて以下の仕様で実装している。

- 揺れ物の挙動が遅延して反映される。
    - 正確に言うと「Transformの更新」→「物理演算」の順で処理を行っているので、物理演算の結果が即時に反映されずに次の更新の呼び出しタイミングで反映される形となっている。
- m_center非対応

Transformの更新タイミングを見直すことで解消できるかもしれないが、現時点では対応していない。



### 導入方法

有効にするには「ENABLE_ECS_SPRING_BONE」と言うシンボルを定義する必要がある。  
→ Assembly Definition側で設定している

※前述の`VRMSpringBoneOptimize-JobSystem`と似通っている部分が多いのでスクリーンショットは割愛。

#### 1. モデルに対する設定

- VRMモデルに設定されている`VRMSpringBone`と`VRMSpringBoneColliderGroup`を"VRMSpringBoneOptimize/Entities/Scripts"以下にある同名のScriptに置き換える。
    - TODO: 後で一括で置き換えてくれる拡張を入れる


#### 2. ECS管理クラスの設定

- 任意のGameObjectに対し`VRMSpringBoneECS`をアタッチ


### 3. Entityの登録/解除について

- `VRMSpringBoneECS`に以下の関数を実装しているので、こちらに対し登録/解除対象の`VRMSpringBone`を渡すこと。
    - 登録 : `VRMSpringBoneECS.AddSpringBone(VRMSpringBone springBone)` or `VRMSpringBoneECS.AddSpringBone(VRMSpringBone[] springBone)`
    - 解除 : `VRMSpringBoneECS.RemoveSpringBone(VRMSpringBone springBone)`
- ※`VRMSpringBoneECS`の初期化タイミングで良ければこちらのInspectorから設定できる`IsAutoGetBuffer`を有効にすることで、MonoBehaviour.Startのタイミングでシーン中に存在する`VRMSpringBone`を自動で集めて登録することが可能。



------------------------------------------------------------------------------------------------

# パフォーマンスについて

以下の環境で[ニコニ立体ちゃんのVRMモデル](https://3d.nicovideo.jp/works/td32797)256体を同時に動かすデモを実装して負荷計測を行ってみたので、参考までに結果について記載していく。

- 実行環境はStandalone(Windows) + IL2CPP
- Unity標準のProfilerで計測
- 途中でモデル1体分の追加/削除を行っている

![performance_demo](https://github.com/mao-test-h/VRMSpringBone-Optimize/blob/feature/dev_update/Documents/img/result/performance_demo.png)


## オリジナル

先ず最初に既存の処理の結果から載せていく。  
MainThreadベースで処理されているLateBehaviourUpdateの負荷が支配的な印象。  
動的なモデルの増減に関してはそこまで負荷が掛かっていない様に見受けられる。  
※Memoryの項目でスパイクが発生している箇所辺りでモデルの追加を行っている。

![original](https://github.com/mao-test-h/VRMSpringBone-Optimize/blob/feature/dev_update/Documents/img/result/original.png)


## VRMSpringBoneOptimize-JobSystem(CentralizedBuffer)

こちらは一括で処理を行うタイプのJobSystemベース実装。  
`IJobParallelForTransform`で物理演算及びTransformの反映を効率よく行えているためか、パフォーマンスとしては4つ挙げた例の中でも一番良い結果となった。  
問題点としては上述のデメリットの項目にもある通り、動的なモデルの追加/削除を行った際にバッファの再構築が入るので負荷が高い。(2つほどある巨大なスパイクがそれ)

![job_centralized](https://github.com/mao-test-h/VRMSpringBone-Optimize/blob/feature/dev_update/Documents/img/result/job_centralized.png)


## VRMSpringBoneOptimize-JobSystem(DistributedBuffer)

こちらはモデル毎に処理を行うタイプのJobSystemベース実装。  
Scheduleの回数が多いためか処理の纏まりが悪く、定期的にスパイクも発生している印象。  
モデルの増減については目立った負荷は見受けられず。  
※こちらもMemoryの項目でスパイクが発生している箇所辺りでモデルの追加を行っているが、追加/削除による負荷は無い様に見受けられる。(定期的に見受けられる青色のスパイクは別の要因で発生しているもの)

![job_distributed](https://github.com/mao-test-h/VRMSpringBone-Optimize/blob/feature/dev_update/Documents/img/result/job_distributed.png)


## VRMSpringBoneOptimize-Entities

最後にECSベースの実行結果。  
`VRMSpringBoneOptimize-JobSystem(CentralizedBuffer)`の結果とまでは行かずとも、近いぐらいのパフォーマンスは出ている。  
モデル追加/削除の負荷についてはCentralizedBufferほどで無いにせよTransform周りのバッファ構築の影響で負荷が掛かっている様に見受けられる。  

![ecs](https://github.com/mao-test-h/VRMSpringBone-Optimize/blob/feature/dev_update/Documents/img/result/ecs.png)




------------------------------------------------------------------------------------------------

# License

- [dwango/UniVRM - LICENSE](https://github.com/dwango/UniVRM/blob/master/LICENSE.txt)



