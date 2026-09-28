using System.Collections;
using TMPro;
using UnityEngine;
using XCharts.Runtime;

public class Past_StockPanelManager : MonoBehaviour
{
    [Header("Main Panels")]
    public GameObject stockListPanel;
    public GameObject realSimulationPanel; // Real_simulation_panel
    public GameObject loadingPanel;

    [Header("Real Simulation Child Panels")]
    public GameObject chartPanel;      // Real_simulation_panel/chart_panel
    public GameObject Buy_Sell_Panel;
    public GameObject Buy_Panel;      // Real_simulation_panel/chart_panel
    public GameObject Sell_Panel;
    public GameObject Chose_Panel;
    public GameObject Buy_Chose_Panel;      // Real_simulation_panel/chart_panel
    public GameObject Sell_Chose_Panel;
    public GameObject Chevron_left;
    public GameObject Real_Buttons;

    public TMP_Text moneytext;
    public TMP_Text historytext;
    public TMP_Text turntext;

    [Header("Loaders")]
    //public StockCandlestickLoader stockLoader;

    [Header("Real Buttons Visuals")]
    public TMP_Text chartButtonText;
    public GameObject chartButtonImage;

    public TMP_Text newsButtonText;
    public GameObject newsButtonImage;


    public float minimumLoadingTime = 0.3f;

    private string currentSymbol = "";

    private bool Buy_Sell_panel_check = false;

    private void Start()
    {
        stockListPanel.SetActive(false);

        realSimulationPanel.SetActive(false);
        chartPanel.SetActive(false);

        StartCoroutine(ShowChapterAfterLoading());

        SetRealButtonVisuals(false, false, false);
    }

    public void Show_Buy_Sell_panel()
    {
        Buy_Sell_panel_check = !Buy_Sell_panel_check;
        Chevron_left.SetActive(!Buy_Sell_panel_check);
        chartPanel.SetActive(!Buy_Sell_panel_check);
        Real_Buttons.SetActive(!Buy_Sell_panel_check);
        Buy_Sell_Panel.SetActive(Buy_Sell_panel_check);
    }

    public void Show_Buy_panel()
    {
        Buy_Panel.SetActive(true);
        Sell_Panel.SetActive(false);
    }
    public void Show_Sell_panel()
    {
        Sell_Panel.SetActive(true);
        Buy_Panel.SetActive(false);
    }

    public void Show_Buy_Chose_panel()
    {
        Chose_Panel.SetActive(true);
        Buy_Chose_Panel.SetActive(true);
    }

    public void Click_Buy()
    {
        moneytext.SetText("보유 현금: 11,600원");
        historytext.SetText("삼성전자/5주/평균단가 680");
        turntext.SetText("2턴 진행중");
        Chose_Panel.SetActive(false);
        Buy_Chose_Panel.SetActive(false);
    }

    public void ShowLoading()
    {
        loadingPanel.SetActive(true);
    }

    public void HideLoading()
    {
        loadingPanel.SetActive(false);
    }

    public void SetCurrentSymbol(string symbol)
    {
        currentSymbol = symbol;
    }

    // 목록으로 돌아가기
    public void OnClickBackToList()
    {
        StartCoroutine(ShowListAfterLoading());
    }

    // 호가 패널 안의 "차트" 버튼에 연결
    public void OnClickBackToChart()
    {
        if (string.IsNullOrEmpty(currentSymbol))
        {
            Debug.LogWarning("현재 선택된 종목 코드가 없습니다.");
            return;
        }

        ShowLoading();


        chartPanel.SetActive(false);

        SetRealButtonVisuals(false, false, false);

        StartCoroutine(ShowChartTabAfterLoading());
    }

    // 차트 패널 안의 "호가" 버튼에 연결
    public void OnClickOrderBook()
    {
        if (string.IsNullOrEmpty(currentSymbol))
        {
            Debug.LogWarning("현재 선택된 종목 코드가 없습니다.");
            return;
        }

        ShowLoading();

        // 차트 캔들 요청만 중지
        // 가격 WebSocket은 유지
        //stockLoader.StopOnlyCandleRequests();

        chartPanel.SetActive(false);

        SetRealButtonVisuals(false, false, false);

        StartCoroutine(ShowOrderBookAfterLoading());
    }

    private IEnumerator ShowOrderBookAfterLoading()
    {

        yield return new WaitForSeconds(minimumLoadingTime);

        realSimulationPanel.SetActive(true);
        chartPanel.SetActive(false);

        // 호가 로딩 완료 후 호가 버튼 텍스트/이미지 보이기
        SetRealButtonVisuals(false, true, false);

        HideLoading();
    }

    public IEnumerator ShowListAfterLoading()
    {
        ShowLoading();

        // 목록으로 나갈 때는 가격 WebSocket까지 종료
        //stockLoader.StopChartRequests();

        chartPanel.SetActive(false);

        // 실시간 시뮬레이션 전체 패널 끄기
        realSimulationPanel.SetActive(false);

        stockListPanel.SetActive(false);

        yield return new WaitForSeconds(minimumLoadingTime);

        stockListPanel.SetActive(true);

        HideLoading();
    }

    public IEnumerator ShowChapterAfterLoading()
    {
        ShowLoading();

        yield return new WaitForSeconds(minimumLoadingTime);

        HideLoading();
    }

    // StockCandlestickLoader의 SearchFlow 마지막에서 호출됨
    public IEnumerator ShowChartAfterLoading()
    {
        ShowLoading();

        stockListPanel.SetActive(false);

        realSimulationPanel.SetActive(false);
        chartPanel.SetActive(false);

        SetRealButtonVisuals(false, false, false);

        yield return new WaitForSeconds(minimumLoadingTime);

        realSimulationPanel.SetActive(true);
        chartPanel.SetActive(true);

        // 차트 로딩 완료 후 차트 버튼 텍스트/이미지 보이기
        SetRealButtonVisuals(true, false, false);

        HideLoading();
    }

    private void SetRealButtonVisuals(bool showChart, bool showBid, bool showInfo)
    {
        if (showChart == true)
        {
            SetTextColor(chartButtonText, "#94C598");
        }
        else
        {
            SetTextColor(chartButtonText, "#000000");
        }
        chartButtonImage.SetActive(showChart);


        if (showBid == true)
        {
            SetTextColor(newsButtonText, "#94C598");
        }
        else
        {
            SetTextColor(newsButtonText, "#000000");
        }
        newsButtonImage.SetActive(showBid);
    }

    public void SetTextColor(TMP_Text text, string hexColor)
    {
        if (ColorUtility.TryParseHtmlString(hexColor, out Color color))
        {
            text.color = color;
        }
    }

    private IEnumerator ShowChartTabAfterLoading()
    {
        //stockLoader.ReloadOnlyCandles();

        yield return new WaitForSeconds(minimumLoadingTime);

        realSimulationPanel.SetActive(true);
        chartPanel.SetActive(true);

        SetRealButtonVisuals(true, false, false);

        HideLoading();
    }

    public void ShowChartTest() {
        stockListPanel.SetActive(false);
        realSimulationPanel.SetActive(true);
        chartPanel.SetActive(true);
    }
}