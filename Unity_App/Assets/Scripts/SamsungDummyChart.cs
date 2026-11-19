using System;
using UnityEngine;
using XCharts.Runtime;

public class SamsungDummyChart : MonoBehaviour
{
    [SerializeField] private BaseChart chart;

    [Header("Dummy Data")]
    [SerializeField] private int tradingDays = 250;
    [SerializeField] private int startPrice = 70000;

    private void Start()
    {
        LoadDummyChart();
    }

    private void LoadDummyChart()
    {
        chart.ClearData();

        // 항상 같은 랜덤 결과가 나오도록 seed 고정
        System.Random random = new System.Random(2026);

        DateTime date = new DateTime(2025, 1, 2);

        double previousClose = startPrice;

        int createdDays = 0;

        while (createdDays < tradingDays)
        {
            // 토요일 / 일요일 제외
            if (
                date.DayOfWeek == DayOfWeek.Saturday ||
                date.DayOfWeek == DayOfWeek.Sunday
            )
            {
                date = date.AddDays(1);
                continue;
            }

            // 전일 종가 기준 시가 변동
            double openChange =
                random.NextDouble() * 0.03 - 0.015;

            double open =
                previousClose * (1.0 + openChange);

            // 당일 종가 변동
            double closeChange =
                random.NextDouble() * 0.05 - 0.025;

            double close =
                open * (1.0 + closeChange);

            // 고가
            double highRate =
                random.NextDouble() * 0.025;

            double high =
                Math.Max(open, close) *
                (1.0 + highRate);

            // 저가
            double lowRate =
                random.NextDouble() * 0.025;

            double low =
                Math.Min(open, close) *
                (1.0 - lowRate);

            // 삼성전자 느낌으로 100원 단위 정리
            open = Round100(open);
            high = Round100(high);
            low = Round100(low);
            close = Round100(close);

            // 비정상 가격 방어
            if (low <= 0)
                low = 100;

            if (high < open)
                high = open;

            if (high < close)
                high = close;

            if (low > open)
                low = open;

            if (low > close)
                low = close;

            AddCandle(
                date.ToString("yy-MM-dd"),
                open,
                high,
                low,
                close
            );

            previousClose = close;

            createdDays++;

            date = date.AddDays(1);
        }

        chart.RefreshChart();
    }

    private double Round100(double value)
    {
        return Math.Round(value / 100.0) * 100.0;
    }

    private void AddCandle(
        string date,
        double open,
        double high,
        double low,
        double close
    )
    {
        chart.AddXAxisData(date);

        // XCharts Candlestick
        // [시가, 종가, 저가, 고가]
        chart.AddData(
            0,
            new double[]
            {
                open,
                close,
                low,
                high
            }
        );
    }
}