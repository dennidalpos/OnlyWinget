namespace OnlyWinget.Application.System;

public interface IUrlProtocolService
{
    bool IsRegistered();

    bool Register(string executablePath);

    bool Unregister();
}
