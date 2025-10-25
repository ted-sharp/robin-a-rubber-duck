using Android.Views;
using AndroidX.RecyclerView.Widget;
using Robin.Models;

namespace Robin.Adapters;

public class MessageAdapter : RecyclerView.Adapter
{
    private readonly List<Message> _messages;
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
            return new UserMessageViewHolder(view);
        }
        else
        {
            var view = inflater.Inflate(Resource.Layout.chat_item_ai, parent, false);
            if (view == null)
                throw new InvalidOperationException("Failed to inflate AI message view");
            return new AIMessageViewHolder(view);
        }
    }

    public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
    {
        var message = _messages[position];

        if (holder is UserMessageViewHolder userHolder)
        {
            userHolder.Bind(message);
        }
        else if (holder is AIMessageViewHolder aiHolder)
        {
            aiHolder.Bind(message);
        }
    }

    public void AddMessage(Message message)
    {
        _messages.Add(message);
        NotifyItemInserted(_messages.Count - 1);
    }

    public void ClearMessages()
    {
        var count = _messages.Count;
        _messages.Clear();
        NotifyItemRangeRemoved(0, count);
    }

    private class UserMessageViewHolder : RecyclerView.ViewHolder
    {
        private readonly TextView _messageText;
        private readonly TextView _timeText;

        public UserMessageViewHolder(View itemView) : base(itemView)
        {
            _messageText = itemView.FindViewById<TextView>(Resource.Id.user_message_text)!;
            _timeText = itemView.FindViewById<TextView>(Resource.Id.user_message_time)!;
        }

        public void Bind(Message message)
        {
            _messageText.Text = message.Content;
            _timeText.Text = message.FormattedTime;
        }
    }

    private class AIMessageViewHolder : RecyclerView.ViewHolder
    {
        private readonly TextView _messageText;
        private readonly TextView _timeText;

        public AIMessageViewHolder(View itemView) : base(itemView)
        {
            _messageText = itemView.FindViewById<TextView>(Resource.Id.ai_message_text)!;
            _timeText = itemView.FindViewById<TextView>(Resource.Id.ai_message_time)!;
        }

        public void Bind(Message message)
        {
            _messageText.Text = message.Content;
            _timeText.Text = message.FormattedTime;
        }
    }
}
