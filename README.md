# VRMSpringBone-Optimize

大量のVRMモデルを動かすことを想定して、[dwango/UniVRM](https://github.com/dwango/UniVRM)に含まれる[VRMSpringBone](https://github.com/dwango/UniVRM/blob/master/Assets/VRM/UniVRM/Scripts/SpringBone/VRMSpringBone.cs)を**C# JobSystem** ベースで最適化してみたもの。

※ パフォーマンスの計測結果諸々については[master](https://github.com/mao-test-h/VRMSpringBone-Optimize)ブランチに有るReadmeを参照。  
※ 実用性を想定してまだ破壊的変更点が多そうなECS対応は一旦オミットしている。



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

### 導入方法

こちらを有効にするには「ENABLE_JOB_SPRING_BONE」と言うシンボルを定義する必要がある。  
→ Assembly Definition側で設定している

使い方については`CentralizedBuffer`と`DistributedBuffer`共に設定について大きな違いは無いので、前者を取り上げる形で説明していく。

#### 1. モデルに対する設定

- VRMモデルの階層以下にあるSpringBoneが管理されているオブジェクト(スクリーンショットの例で言えば`secondary`)に`CentralizedBuffer`をアタッチ
- VRMモデルに設定されている`VRMSpringBone`と`VRMSpringBoneColliderGroup`を"VRMSpringBoneOptimize/Jobs/Scripts"以下にある同名のScriptに置き換える。
    - ※置き換え用の拡張を実装してある。メニューにある「VRMSpringBoneOptimize/Replace SpringBone Components - Jobs」を実行することで、Scene中にあるVRMモデルに対し一括で上記2点をJobSystem向けのものに置き換える事が可能。

![job_model_settings1](https://github.com/mao-test-h/VRMSpringBone-Optimize/blob/master/Documents/img/job_model_settings1.png)


#### 2. スケジューラーの生成

- 任意のGameObjectに対し`CentralizedJobScheduler`をアタッチ
    - ※画像の例だと管理クラス用に`Scheduler`と言うGameObjectを用意してそちらにアタッチしている。

![job_model_settings2](https://github.com/mao-test-h/VRMSpringBone-Optimize/blob/master/Documents/img/job_model_settings2.png)


### 3. Jobの登録/解除について

- `CentralizedJobScheduler`に以下の関数を実装しているので、こちらに対し登録/解除対象の`CentralizedBuffer`を渡すこと。
    - 登録 : `CentralizedJobScheduler.AddBuffer(`CentralizedJobScheduler`)`
    - 解除 : `CentralizedJobScheduler.RemoveBuffer(`CentralizedJobScheduler`)`
- ※`CentralizedJobScheduler`の初期化タイミングで良ければこちらのInspectorから設定できる`IsAutoGetBuffer`を有効にすることで、MonoBehaviour.Startのタイミングでシーン中に存在する`CentralizedJobScheduler`を自動で集めて登録することが可能。



------------------------------------------------------------------------------------------------

# License

- [dwango/UniVRM - LICENSE](https://github.com/dwango/UniVRM/blob/master/LICENSE.txt)


