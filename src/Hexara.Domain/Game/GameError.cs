namespace Hexara.Domain.Game;

/// <summary>
/// دلیل رد شدن یک حرکت. کددار است تا لایه‌ی وب بتواند مستقیم به کلید ترجمه
/// نگاشتش کند و متن خطا در دامنه نباشد.
/// </summary>
public enum GameError
{
    None = 0,

    NotYourTurn,
    WrongPhase,
    GameFinished,

    VertexNotOnBoard,
    EdgeNotOnBoard,
    HexNotOnBoard,

    VertexOccupied,
    TooCloseToAnotherBuilding,
    EdgeOccupied,
    RoadNotConnected,
    SettlementNotConnectedToRoad,
    SetupRoadMustTouchSettlement,

    NotEnoughResources,
    NoPiecesLeft,
    NotYourSettlement,
    NotASettlement,

    RobberMustChangeHex,
    InvalidVictim,
    VictimRequired,

    NothingToDiscard,
    WrongDiscardAmount,
    NotEnoughCardsToDiscard,

    DevelopmentDeckEmpty,
    NoSuchDevelopmentCard,
    CardBoughtThisTurn,
    AlreadyPlayedADevelopmentCard,
    VictoryPointCardIsNotPlayable,

    BankCannotPay,
    NoPortForThisRate,
    CannotTradeTheSameResource,
    EmptyTrade,

    TradeAlreadyOnTheTable,
    NoTradeOnTheTable,
    NotYourTrade,
    NotInvitedToTrade,
    PartnerDidNotAccept
}
