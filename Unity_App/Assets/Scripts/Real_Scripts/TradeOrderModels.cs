using System;
using System.Globalization;

public static class OrderTimeFormat
{
    // Server timestamps are UTC; show Korea time regardless of device timezone.
    public static string Korea(string value, string format = "yyyy-MM-dd HH:mm:ss")
    {
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var utc)) return "-";
        return utc.ToOffset(TimeSpan.FromHours(9)).ToString(format, CultureInfo.InvariantCulture);
    }
}

[Serializable]
public class TradeOrderData
{
    public string _id, userId, symbol, name, market, side, orderType, status, createdAt;
    public int quantity, filledQuantity, reservedQuantity, version;
    public long orderPrice, reservedAmount;
    public long? limitPrice, executedPrice;
    public bool CanEdit => status == "PENDING" && orderType == "LIMIT" && filledQuantity == 0;
    public string SideLabel => side == "BUY" ? "매수" : "매도";
    public string StatusLabel => status == "PENDING" ? "미체결" : status == "FILLED" ? "체결" :
                                 status == "CANCELED" ? "취소" : "거절";
}

[Serializable]
public class TradeOrdersResponse
{
    public bool success;
    public TradeOrderData[] data;
}

[Serializable]
public class TradeOrderResult
{
    public bool success;
    public TradeOrderData data;
}
