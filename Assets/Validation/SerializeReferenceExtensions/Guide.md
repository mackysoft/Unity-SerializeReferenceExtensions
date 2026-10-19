# Inspector操作の比較ウィンドウと検証用シーン

このフォルダーは既存Unityプロジェクト内の手動確認用データ。配布パッケージ、Example、自動テストには含めない。シーンへ保存するデータはUnityEngine側のassembly、Drawerと操作メニューはEditor専用assemblyに分け、プロジェクトにある実際のSerializeReferenceExtensionsを参照する。

`Tools > SerializeReference Extensions > Validation > Open Comparison`で左右の比較ウィンドウを開く。`Open Scene`で既存の検証シーンを開き、Hierarchyで対象を選んで`Use Selection`を押す。選択はボタンを押した時に取り込むため、別のオブジェクトを選択しても比較対象は切り替わらない。同じ型の複数コンポーネントも取り込める。

- `Compare: Unity Standard And Subclass Selector`：左はルートのSubclassSelector属性を外した一時コピー、右は実シーン／Prefabのオブジェクト。`Backend`で両列をIMGUIまたはUI Toolkitへ切り替える。初期値、フィールドの型、Header・Space・Tooltip・NonReorderable、値の型に付いたDrawerは共通。NestedValue内部の属性も型が所有するため共通。
- `Compare: IMGUI And UI Toolkit`：同じ実オブジェクトを左のIMGUIと右のUI Toolkitで操作する。編集、Undo/Redo、展開状態は同じ対象へ作用する。
- `Refresh Standard Copy`：現在の実データと展開状態を左の比較用コピーへ取り直す。コピー側の編集はシーンへ保存されず、Prefabのoverrideも持たない。PrefabのApply/Revertと複数選択は実オブジェクトを描く列で確認する。

両列は実際の`InspectorElement`を使う。ウィンドウのリサイズではInspectorや比較データを作り直さない。ラベル幅・字下げ・余白はUnityに任せ、独自の寸法補正を入れない。

シーンだけを開く場合は`Tools > SerializeReference Extensions > Validation > Open Scene`、または `Scenes/InspectorValidation.unity`を開く。メニューはシーンが存在しない場合だけ初期データを作成し、既存の編集内容をリセットしない。

| Hierarchy | 確認するもの |
| --- | --- |
| 01 UI Toolkit Inspector | `InspectorElement.FillDefaultInspector`による標準UI Toolkit Inspector |
| 02 IMGUI Inspector | `DrawDefaultInspector`による標準IMGUI Inspector。同じ基底コンポーネントの同じフィールド構成 |
| 03 Prefab instance | 実Prefabに対するReferenceの初期override。太字、override表示、Apply、Revert、Undo/Redo |
| 04 / 05 Multi selection | 異なる型を持つ2オブジェクト。同時選択してmixed表示と一括編集を確認 |

各InspectorにはUnity標準のreference・Array/List・NonReorderableと、SubclassSelector付きの同じ初期データを置いている。ラベル、入力欄、矢印、インデント、右端、展開時の高さ、Inspector幅の変更は同じ版・同じ描画方式の標準と比較する。独自のUSSや幅・余白・インデント補正は使わない。

## 操作箇所

- Reference / Null Reference / Empty Value：型メニューの検索・階層・null・フィールドなしの型。`HiddenValue`は候補に出ない。
- Described / List：`UseToStringAsLabel`と編集後のラベル更新。
- Array / List：3要素の中央がnull。開閉、null行の表示・移動、追加・削除・並び替え、Undo/Redo。
- Simple Array / Simple List：`NonReorderable`の標準構成。並び替えハンドルの有無も標準と比較。
- Empty Array / Empty List：空のcollectionから追加し、削除して空に戻す。
- Nested：子referenceと4種のcollection。親子の開閉と編集。
- Other Property Drawers：IMGUIのみ、UI Toolkitのみ、両API、継承先への`useForChildren`、collection内での組合せ。本文のマーカーで呼ばれたAPIを見分ける。
- Decorated：Header、Space、Tooltip、TextArea。
- 型入力やラベルの右クリック：Copy / Paste、New Instance、Reset、Prefab操作。実行前後の値とUndo/Redoを確認する。

IMGUI InspectorではUI ToolkitのみのDrawerを実行できない。Dual Drawerの本文は、このライブラリの既存OnGUI優先契約に従う。これらを、実行していないAPIの成功と扱わない。

Unity標準側の差もそのまま表示する。例として2021.3の標準UI Toolkitにはnull managed-reference行の欠落と、それに伴うSimple collectionの高さ更新停止があり、Simple行の右端clipも標準側に発生する。比較のために標準側を補正しない。

シーンとPrefabはUnity APIで生成した通常のassetであり、任意の状態を保存して再現に使える。初期状態へ戻す場合は、意図したassetだけをバージョン管理から復元する。自動で全シーンを書き換える処理や、起動時の初期化はない。
