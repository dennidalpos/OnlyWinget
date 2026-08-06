using CommunityToolkit.Mvvm.Messaging.Messages;
using OnlyWinget.Application.App;

namespace OnlyWinget.Presentation;

public sealed class StateChangedMessage(OnlyWingetState state) : ValueChangedMessage<OnlyWingetState>(state)
{
}
