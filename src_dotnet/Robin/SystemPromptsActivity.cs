using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.AppCompat.App;

namespace Robin;

/// <summary>
/// システムプロンプト設定メニュー画面
/// 会話用プロンプトと意味解析プロンプットの編集画面を選択
/// </summary>
[Activity(Label = "システムプロンプト設定", Theme = "@style/AppTheme")]
public class SystemPromptsActivity : AppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // プロンプト選択メニューを表示
        ShowPromptSelectionMenu();
    }

    /// <summary>
    /// プロンプト選択メニューを表示
    /// </summary>
    private void ShowPromptSelectionMenu()
    {
        var items = new[]
        {
            "会話用プロンプト",
            "意味解析プロンプト",
            "ユーザーコンテキスト"
        };

        var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
        builder.SetTitle("プロンプト設定");
        builder.SetItems(items, (sender, args) =>
        {
            switch (args.Which)
            {
                case 0:
                    // 会話用プロンプト編集画面へ
                    var conversationIntent = new Intent(this, typeof(ConversationPromptActivity));
                    StartActivity(conversationIntent);
                    Finish();
                    break;
                case 1:
                    // 意味解析プロンプト編集画面へ
                    var semanticIntent = new Intent(this, typeof(SemanticValidationPromptActivity));
                    StartActivity(semanticIntent);
                    Finish();
                    break;
                case 2:
                    // ユーザーコンテキスト編集画面へ
                    var contextIntent = new Intent(this, typeof(UserContextActivity));
                    StartActivity(contextIntent);
                    Finish();
                    break;
            }
        });
        builder.SetNegativeButton("キャンセル", (sender, args) => Finish());
        builder.SetCancelable(false);
        builder.Show();
    }
}
