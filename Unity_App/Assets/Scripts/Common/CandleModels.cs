using System;
using System.Collections.Generic;

[Serializable]
public class CandlePoint
{
    public string date;
    public int open;
    public int high;
    public int low;
    public int close;
}

[Serializable]
public class CandleResponse
{
    public string symbol;
    public List<CandlePoint> points;
}

[Serializable]
public class StockInfoResponse
{
    public string symbol;
    public string name;
    public string market;

    public int price;
    public int change;
    public double change_rate;
    public string change_sign;

    public int open_price;
    public int high_price;
    public int low_price;
    public long volume;
}

[Serializable]
public class TradeRequest
{
    public string symbol;
    public int quantity;
    public string order_type;
    public int? limit_price;
    public string client_request_id;
}

[Serializable]
public class TradeResponse
{
    public string message;
    public string symbol;
    public int quantity;
    public int price;
    public long cash;
    public string order_id;
    public string status;
    public int filled_quantity;
}

[Serializable]
public class Holding
{
    public string symbol;
    public string name;
    public int quantity;
    public double avg_price;
    public int reservedQuantity;
    public int availableQuantity;
}

[Serializable]
public class PortfolioResponse
{
    public long cash;
    public long reservedCash;
    public long availableCash;
    public List<Holding> holdings;
}

[Serializable]
public class RealtimePriceMessage
{
    public string symbol;
    public int price;

    public int change;
    public double change_rate;
    public string change_sign;

    public CandlePoint candle;
}

[System.Serializable]
public class StockDetailResponse
{
    public string symbol;

    public int open_price;
    public int high_price;
    public int low_price;
    public int volume;

    public int week52_high;
    public int week52_low;
    public int upper_limit;
    public int lower_limit;

    public long market_cap;
    public float per;
    public float pbr;
    public int eps;
    public int bps;
    public float roe;
    public long revenue;
    public long operating_profit;

    public string individual_supply;
    public string foreign_supply;
    public string institution_supply;
}
