# DieselCompiler

## 概要

`DieselCompiler` は、C 風の簡潔なスクリプト言語を Diesel 式に変換するコンパイラです。

## 使用できる命令

| 機能                  | スクリプト構文                         | 生成される Diesel コード                    |
| --------------------- | -------------------------------------- | ------------------------------------------- |
| コメント              | `// Comment`                           | コメント                                    |
| 変数宣言              | `env x`                                | x を 変数 として登録                        |
| 環境変数宣言          | `var x`                                | x を システム変数 として登録                |
| 実行                  | `:`                                    | `;`                                         |
| 標準出力              | `dout << Comments; `                   | `Comments^X`                                |
| 標準入力（変数）      | `din >> x `                            | `setvar;x;\`                                |
| 標準入力              | `input`                                | `\`                                         |
| 代入                  | `x = 10`                               | `setvar;x;10;`                              |
| 式                    | `l = 2 * r * 3.14`                     | `setenv;l;$M=$(*,$(*,2,$(getenv,r)),3.14);` |
| 条件分岐              | `if( x < 0 ){foo}{bar} `               | `$M=$(if,$(<,$(getenv,x),0),foo,bar)`       |
| switch ステートメント | `nth(to){case{foo}case{bar}case{baz}}` | `$M=$(nth,$(getenv,to),foo,bar,baz)`        |
| 繰り返し              | `Loop`                                 | `*^C^C`                                     |

### 宣言

識別子は必ず宣言してください。コンパイラが変数と環境変数を区別できません。

```
env cmd;
var cmdactive;
```

## コード例

### スクリプト言語

```
cancel                // ^C^C コマンドを解除

// ---- 入力値の取得 ----
env 横;
env 縦;
din >> 横;
din >> 縦;


// ---- 作図コマンド ----
_id:;                    // １点目を指示
input;
_rectang:;
non:@横 / 2,縦 / 2:;    // ２点目を相対座標で入力
non:@-横,-縦:;
```

### コンパイル後

```
^C^Csetenv;横;\setenv;縦;\_id;\_rectang;non;@$M=$(/,$(getenv,横),2),$M=$(/,$(getenv,縦),2);non;@-$M=$(getenv,横),-$M=$(getenv,縦);
```

### スクリプト言語

```
cancel;

area:o:;
input;

env 直径;
var perimeter;

直径 = perimeter / 3.141592654;
din >> 直径

change:
@:::
直径 / 2:;
```

### コンパイル後

```
^C^Carea;o;\setenv;直径;$M=$(/,$(getvar,perimeter),3.141592654);setenv;直径;\change;@;;;$M="$(/,$(getenv,直径),2)";
```

### スクリプト言語

```
var cmdactive;
env selected;
env cmd;
env sl;

if(cmdactive){	// コマンドが実行中かどうか
	cancel;		// 実行していたらいったんキャンセル
	select:;
	if(selected){ 
		p::;	// 事前に選択されていたらそれを選択
	}else{ 
		sl = 1;	// そうでない場合、slフラグを立てる
	}
}else{
	select:;
	if(cmdactive){ //選択されていないとselectコマンドが立ち上がったまま,つまりcmdactive == 1
		sl = 1;	// 選択されていない場合, slフラグを立てる
	}
}

cmd = cmd | 4;

if(sl){			// slフラグが立っているなら,
	sl = 0;
	selected = 0;
	input;		// ユーザー入力をする
}
selected = 1;	// 入力が終わったということはオブジェクトが選択された

nth(cmd & 3){	
	case{
	}
	case{
		m;
	}
	case{
		co:p:m:
	}
	case{
		s;
	}
}

p::
input;
.y:;
@:;
```

### コンパイル後

```
$M=$(if,$(getvar,cmdactive),^C^Cselect;$M="""$(if,$(getenv,selected),p;;,'setenv;sl;1;)""",select;$M="""$(if,$(getvar,cmdactive),'setenv;sl;1;,)""")'setenv;cmd;$(or,$(getenv,cmd),4);$M="""$(if,$(getenv,sl),'setenv;sl;0;'setenv;selected;0;\,)"""'setenv;selected;1;$(nth,$(and,$(getenv,cmd),3),,m,co;p;m;,s)p;;\.y;@;
```
