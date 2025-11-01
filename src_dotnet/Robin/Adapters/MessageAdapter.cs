using Android.Graphics;
using Android.Views;
using AndroidX.RecyclerView.Widget;
using Robin.Models;

namespace Robin.Adapters;

public sealed class MessageAdapter : RecyclerView.Adapter
{
    private readonly List<Message> _messages;
    private readonly HashSet<int> _selectedMessagesForDeletion = new();
    private const int ViewTypeUser = 1;
    private const int ViewTypeAssistant = 2;

    public MessageAdapter(List<Message> messages)
    {
        _messages = messages;
    }

    public override int ItemCount => _messages.Count;

    public override int GetItemViewType(int position)
    {
        return _messages[position].IsUser ? ViewTypeUser : ViewTypeAssistant;
    }

    public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
    {
        var inflater = LayoutInflater.From(parent.Context);
        if (inflater == null)
            throw new InvalidOperationException("LayoutInflater is null");

        if (viewType == ViewTypeUser)
        {
            var view = inflater.Inflate(Resource.Layout.chat_item_user, parent, false);
            if (view == null)
                throw new InvalidOperationException("Failed to inflate user message view");
            return new UserMessageViewHolder(view, this);
        }
        else
        {
            var view = inflater.Inflate(Resource.Layout.chat_item_ai, parent, false);
            if (view == null)
                throw new InvalidOperationException("Failed to inflate AI message view");
            return new AIMessageViewHolder(view, this);
        }
    }

    public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
    {
        var message = _messages[position];
        var isDeleteButtonVisible = _selectedMessagesForDeletion.Contains(position);

        if (holder is UserMessageViewHolder userHolder)
        {
            userHolder.Bind(message);
            userHolder.SetupDeleteButton(position, isDeleteButtonVisible, this);

            // 前のメッセージがユーザーメッセージの場合、上部の padding を削減
            if (position > 0 && _messages[position - 1].IsUser)
            {
                userHolder.SetCompactSpacing(true);
            }
            else
            {
                userHolder.SetCompactSpacing(false);
            }
        }
        else if (holder is AIMessageViewHolder aiHolder)
        {
            aiHolder.Bind(message);
            aiHolder.SetupDeleteButton(position, isDeleteButtonVisible, this);

            // 前のメッセージが AI メッセージの場合、上部の padding を削減
            if (position > 0 && _messages[position - 1].IsAssistant)
            {
                aiHolder.SetCompactSpacing(true);
            }
            else
            {
                aiHolder.SetCompactSpacing(false);
            }
        }
    }

    public void AddMessage(Message message)
    {
        _messages.Add(message);
        NotifyItemInserted(_messages.Count - 1);
    }

    /// <summary>
    /// 指定インデックスのメッセージを更新（2段階表示対応）
    /// </summary>
    public void UpdateMessage(int position, Message message)
    {
        if (position >= 0 && position < _messages.Count)
        {
            _messages[position] = message;
            NotifyItemChanged(position);
        }
    }

    public void ClearMessages()
    {
        var count = _messages.Count;
        _messages.Clear();
        _selectedMessagesForDeletion.Clear();
        NotifyItemRangeRemoved(0, count);
    }

    /// <summary>
    /// 指定インデックスのメッセージの削除ボタン表示状態をトグル
    /// </summary>
    public void ToggleDeleteButton(int position)
    {
        if (_selectedMessagesForDeletion.Contains(position))
        {
            _selectedMessagesForDeletion.Remove(position);
        }
        else
        {
            _selectedMessagesForDeletion.Add(position);
        }
        NotifyItemChanged(position);
    }

    /// <summary>
    /// 指定インデックスのメッセージを削除
    /// </summary>
    public void DeleteMessage(int position)
    {
        if (position >= 0 && position < _messages.Count)
        {
            _messages.RemoveAt(position);
            _selectedMessagesForDeletion.Remove(position);
            NotifyItemRemoved(position);

            // インデックスが変わったので、position以降の選択状態を更新
            var itemsToUpdate = _selectedMessagesForDeletion.Where(idx => idx > position).ToList();
            foreach (var idx in itemsToUpdate)
            {
                _selectedMessagesForDeletion.Remove(idx);
                _selectedMessagesForDeletion.Add(idx - 1);
            }
        }
    }

    private sealed class UserMessageViewHolder : RecyclerView.ViewHolder
    {
        private readonly TextView _messageText;
        private readonly LinearLayout _rootLayout;
        private readonly LinearLayout _messageBubble;
        private readonly ImageButton _deleteButton;
        private EventHandler? _messageBubbleClickHandler;
        private EventHandler? _deleteButtonClickHandler;

        public UserMessageViewHolder(View itemView, MessageAdapter adapter) : base(itemView)
        {
            _rootLayout = (LinearLayout)itemView;
            _messageText = itemView.FindViewById<TextView>(Resource.Id.user_message_text)!;
            _messageBubble = itemView.FindViewById<LinearLayout>(Resource.Id.message_bubble)!;
            _deleteButton = itemView.FindViewById<ImageButton>(Resource.Id.delete_button)!;
        }

        public void Bind(Message message)
        {
            // GetDisplayContent() で表示内容を取得
            // 修正情報がある場合は含める
            if (message.WasCorrected)
            {
                _messageText.Text = message.GetDetailedContent();
            }
            else
            {
                _messageText.Text = message.GetDisplayContent();
            }

            // 意味解析済みの場合は色を変更
            if (message.IsSemanticValidationApplied)
            {
                if (message.WasCorrected)
                {
                    // 修正が行われた場合は青色（情報通知）
                    _messageText.SetTextColor(Color.ParseColor("#2196F3")); // 青
                }
                else
                {
                    // 修正が不要だった場合は緑色（検証OK）
                    _messageText.SetTextColor(Color.ParseColor("#4CAF50")); // 緑
                }
            }
            else if (message.DisplayState == MessageDisplayState.ValidatingSemantics)
            {
                _messageText.SetTextColor(Color.ParseColor("#FF9800")); // オレンジ（検証中）
            }
            else
            {
                _messageText.SetTextColor(Color.ParseColor("#212121")); // デフォルト黒
            }
        }

        public void SetupDeleteButton(int position, bool isVisible, MessageAdapter adapter)
        {
            // 削除ボタンの表示状態を設定
            _deleteButton.Visibility = isVisible ? ViewStates.Visible : ViewStates.Gone;

            // 前回のハンドラを削除
            if (_messageBubbleClickHandler != null)
            {
                _messageBubble.Click -= _messageBubbleClickHandler;
            }
            if (_deleteButtonClickHandler != null)
            {
                _deleteButton.Click -= _deleteButtonClickHandler;
            }

            // メッセージバブルのクリックハンドラを設定
            _messageBubbleClickHandler = (sender, e) =>
            {
                adapter.ToggleDeleteButton(position);
            };
            _messageBubble.Click += _messageBubbleClickHandler;

            // 削除ボタンのクリックハンドラを設定
            _deleteButtonClickHandler = (sender, e) =>
            {
                adapter.DeleteMessage(position);
            };
            _deleteButton.Click += _deleteButtonClickHandler;
        }

        public void SetCompactSpacing(bool isCompact)
        {
            int topPadding = isCompact ? 0 : (int)(2 * _rootLayout.Resources!.DisplayMetrics!.Density);
            _rootLayout.SetPadding(
                _rootLayout.PaddingLeft,
                topPadding,
                _rootLayout.PaddingRight,
                _rootLayout.PaddingBottom
            );
        }
    }

    private sealed class AIMessageViewHolder : RecyclerView.ViewHolder
    {
        private readonly TextView _messageText;
        private readonly LinearLayout _rootLayout;
        private readonly LinearLayout _messageBubble;
        private readonly ImageButton _deleteButton;
        private EventHandler? _messageBubbleClickHandler;
        private EventHandler? _deleteButtonClickHandler;

        public AIMessageViewHolder(View itemView, MessageAdapter adapter) : base(itemView)
        {
            _rootLayout = (LinearLayout)itemView;
            _messageText = itemView.FindViewById<TextView>(Resource.Id.ai_message_text)!;
            _messageBubble = itemView.FindViewById<LinearLayout>(Resource.Id.message_bubble)!;
            _deleteButton = itemView.FindViewById<ImageButton>(Resource.Id.delete_button)!;
        }

        public void Bind(Message message)
        {
            _messageText.Text = message.GetDisplayContent();
        }

        public void SetupDeleteButton(int position, bool isVisible, MessageAdapter adapter)
        {
            // 削除ボタンの表示状態を設定
            _deleteButton.Visibility = isVisible ? ViewStates.Visible : ViewStates.Gone;

            // 前回のハンドラを削除
            if (_messageBubbleClickHandler != null)
            {
                _messageBubble.Click -= _messageBubbleClickHandler;
            }
            if (_deleteButtonClickHandler != null)
            {
                _deleteButton.Click -= _deleteButtonClickHandler;
            }

            // メッセージバブルのクリックハンドラを設定
            _messageBubbleClickHandler = (sender, e) =>
            {
                adapter.ToggleDeleteButton(position);
            };
            _messageBubble.Click += _messageBubbleClickHandler;

            // 削除ボタンのクリックハンドラを設定
            _deleteButtonClickHandler = (sender, e) =>
            {
                adapter.DeleteMessage(position);
            };
            _deleteButton.Click += _deleteButtonClickHandler;
        }

        public void SetCompactSpacing(bool isCompact)
        {
            int topPadding = isCompact ? 0 : (int)(2 * _rootLayout.Resources!.DisplayMetrics!.Density);
            _rootLayout.SetPadding(
                _rootLayout.PaddingLeft,
                topPadding,
                _rootLayout.PaddingRight,
                _rootLayout.PaddingBottom
            );
        }
    }
}
