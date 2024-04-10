namespace OBD.NET.Commands;

public class ATCommand
{
    #region Commands

    public static readonly ATCommand RepeatLastCommand = new("\r");
    public static readonly ATCommand ResetDevice = new("ATZ");
    public static readonly ATCommand ReadVoltage = new("ATRV");
    public static readonly ATCommand EchoOn = new("ATE1", "^OK$");
    public static readonly ATCommand EchoOff = new("ATE0", "^OK$");
    public static readonly ATCommand HeadersOn = new("ATH1", "^OK$");
    public static readonly ATCommand HeadersOff = new("ATH0", "^OK$");
    public static readonly ATCommand PrintSpacesOn = new("ATS1", "^OK$");
    public static readonly ATCommand PrintSpacesOff = new("ATS0", "^OK$");
    public static readonly ATCommand LinefeedsOn = new("ATL1", "^OK$");
    public static readonly ATCommand LinefeedsOff = new("ATL0", "^OK$");
    public static readonly ATCommand SetProtocolAuto = new("ATSP0", "^OK$");
    public static readonly ATCommand PrintVersion = new("ATI", "^ELM327.*");
    public static readonly ATCommand CloseProtocol = new("ATPC");

    #endregion

    #region Properties & Fields

    public string Command { get; }
    public string? ExpectedResult { get; }

    #endregion

    #region Constructors

    private ATCommand(string command, string? expectedResult = null)
    {
        Command = command;
        ExpectedResult = expectedResult;
    }

    #endregion

    #region Methods

    public override string ToString() => Command;

    #endregion

    #region Operators

    public static implicit operator string(ATCommand command) => command.ToString();

    #endregion
}