# GraphPlotter 2
燃焼実験によって得られたデータを解析、及び描画するプログラムです。
加工済みデータをCSV、PNG、JPGで出力可能です。

## How to use
1. 起動して、`Files`->`Open...`を選択したのち、ファイル形式にあった方を選択してください。
2. 描画されます。
3. 追加でもう一つだけ読み込むことも可能です。
4. `Files`->`Save...`からデータを保存できます。
5. `Settings`から設定を開くことができます。
6. `Files`->`InitScreen`を押すとグラフを全削除できます。

## 読み込むファイルについて
csvまたはbinファイルを読み込んでください。なおフォーマットは以下の通りである必要があります。

### CSVフォーマット
一列目に時間、二列目に電圧値(推力の値)

時間は逆行している状態をなるべく避けてください。(逆行していた場合自動でソートしますが、ほとんど正しく並べられていることを前提としているため、あまりにも多くのデータが逆行していると非常に時間がかかります。)

### BINフォーマット
前回データからの経過時間(つまり $d T$ )、電圧(推力の値)を交互に繰り返すようなバイナリファイル

前回データからの経過時間、電圧ともに4バイト(32bit)で、時間データは符号なし整数、電圧は符号付き整数である必要があります。

## 注意事項
1. 時間が逆行していた場合、自動でソートされます。
2. 大きな外れ値や、データエラーがあるとうまく動作しない可能性があります。
3. 外れ値等の原因により何かしらの問題が発生した場合、自動で問題のある箇所のデータが切り捨てられます。
4. 太く表示されているグラフはノイズ除去アルゴリズムによりデノイズしたものになります。元々のデータは薄く後ろに描画されます。なお、デノイズに用いたアルゴリズムはサビツキーゴーレイフィルタです。幅は21で、4次関数による近似を用いたデノイズです。

## 設定の説明
### GraphContents
グラフに描画する要素を選択できます。

### Other
各設定項目において最後に(CSV)や(BIN)と書いてあった場合、読み込むデータにより読み込む設定を変えられます。
#### Main(sub)GraphName
グラフの名前です。グラフのタイトルや、全力積の表示に用いられます。

#### SubGraphOpacity[%]
2つデータを読み込んだとき、2個めのデータの透明度を調整できます。

#### UndenoisedGraphOpacity[%]
後ろに表示される、デノイズする前の元々のデータの透明度を調整できます。

#### BurningTimeOpacity[%]
燃焼時間中はグラフが薄く塗りつぶされていますが、その塗りつぶしの透明度を調整できます。

#### Slope
検量線を表す関数 $Ax+B$ における定数 $A$ 。即ち傾き。

#### Intercept
検量線を表す関数 $Ax+B$ における定数 $B$ 。即ち切片。

#### IgnitionDetectionThreshold[%]
最大推力の何%を超えたとき燃焼開始と判定するかを調整できます。整数のみ対応しています。
なお、正確には最大推力を観測地点から遡って行き、最大推力のIgnitionDetectionThreshold%を連続で20回下回ったとき、下回り始めた箇所を燃焼開始地点と判定するようになっています。

#### BurnoutDetectionThreshold[%]
最大推力の何%を下回ったとき燃焼終了と判定するかを調整できます。整数のみ対応しています。
なお、正確には最大推力を観測地点から辿って行き、最大推力のBurnoutDetectionThreshold%を連続で20回下回ったとき、下回り始めた箇所を燃焼終了地点と判定するようになっています。

#### Prefix of Time
燃焼実験ログの時間データの単位を入力してください。例えばmsであれば0.001であり、μmであれば0.000001としてください。

## 全力積の計算式について
燃焼開始から終了までのN個のデータ $[0, N-1]$ が存在する。時間データを $T_n$ 、推力データを $F_n$ とするとき、次の計算式で全力積を計算している。
$$I = \left(\sum_{k=0}^{N-2} (F_{k+1} + F_k)(T_{k+1} - T_k)\right) / 2$$
