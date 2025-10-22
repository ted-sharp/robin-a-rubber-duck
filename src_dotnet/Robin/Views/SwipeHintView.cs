using Android.Content;
using Android.Graphics;
using Android.Runtime;
using Android.Util;
using Android.Views;

namespace Robin.Views;

/// <summary>
/// ドロワーのスワイプヒントを表示するビュー
/// 画面左端に山形矢印（〉）を表示してスワイプ可能を示す
/// タッチイベントは処理せず、視覚的なヒントのみ
/// </summary>
public class SwipeHintView : View
{
    private readonly Paint _arrowPaint;
    private readonly Android.Graphics.Path _arrowPath;

    public SwipeHintView(Context context) : this(context, null)
    {
    }

    public SwipeHintView(Context context, IAttributeSet? attrs) : this(context, attrs, 0)
    {
    }

    public SwipeHintView(Context context, IAttributeSet? attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
    {
        // 矢印の描画用ペイント
        _arrowPaint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.Argb(100, 33, 150, 243), // 半透明のプライマリカラー
            StrokeWidth = 3 * (context.Resources?.DisplayMetrics?.Density ?? 1f)
        };
        _arrowPaint.SetStyle(Paint.Style.Stroke);
        _arrowPaint.StrokeCap = Paint.Cap.Round;
        _arrowPaint.StrokeJoin = Paint.Join.Round;

        _arrowPath = new Android.Graphics.Path();
    }

    protected SwipeHintView(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
    {
        _arrowPaint = new Paint(PaintFlags.AntiAlias);
        _arrowPath = new Android.Graphics.Path();
    }

    protected override void OnSizeChanged(int w, int h, int oldw, int oldh)
    {
        base.OnSizeChanged(w, h, oldw, oldh);

        // 山形矢印（〉）のパスを作成
        _arrowPath.Reset();

        var centerY = h / 2f;
        var density = Context?.Resources?.DisplayMetrics?.Density ?? 1f;
        var arrowSize = 16 * density; // 矢印のサイズ

        // 二重の山形矢印を描画
        // 1つ目の矢印
        _arrowPath.MoveTo(2, centerY - arrowSize);
        _arrowPath.LineTo(arrowSize, centerY);
        _arrowPath.LineTo(2, centerY + arrowSize);

        // 2つ目の矢印（少し右にオフセット）
        _arrowPath.MoveTo(arrowSize + 4, centerY - arrowSize);
        _arrowPath.LineTo(arrowSize * 2 + 2, centerY);
        _arrowPath.LineTo(arrowSize + 4, centerY + arrowSize);
    }

    protected override void OnDraw(Canvas? canvas)
    {
        base.OnDraw(canvas);

        if (canvas == null)
            return;

        // 矢印を描画
        canvas.DrawPath(_arrowPath, _arrowPaint);
    }

    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        // 固定幅、親の高さに合わせる
        var density = Context?.Resources?.DisplayMetrics?.Density ?? 1f;
        var width = (int)(40 * density); // 40dp
        var height = MeasureSpec.GetSize(heightMeasureSpec);
        SetMeasuredDimension(width, height);
    }

    public override bool OnTouchEvent(MotionEvent? e)
    {
        // タッチイベントは処理せず、親に委譲
        return false;
    }
}
