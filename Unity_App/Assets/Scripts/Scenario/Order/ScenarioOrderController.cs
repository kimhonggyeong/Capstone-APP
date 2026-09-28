using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.UI;

public class ScenarioOrderController : MonoBehaviour
{
    // 턴 종료 패널에서 이번 턴 주문 내역을 받기 위한 이벤트
    public event Action<ScenarioOrderSummary> OnOrderCompleted;

    public IReadOnlyList<ScenarioOrderSummary> CurrentTurnOrders =>
        currentTurnOrders;

    [Header("서버 - ScenarioDataManager와 동일하게 설정")]

    [Header("매수 / 매도")]
    [SerializeField] private Button buyButton;
    [SerializeField] private Button sellButton;
    [SerializeField] private TMP_Text buyText;
    [SerializeField] private TMP_Text sellText;

    [SerializeField]
    private Color selectedColor =
        new Color32(255, 107, 26, 255);

    [SerializeField]
    private Color normalColor = Color.gray;

    [Header("수량 및 금액")]
    [SerializeField] private TMP_InputField quantityInput;
    [SerializeField] private Button minusButton;
    [SerializeField] private Button plusButton;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text estimatedAmountText;
    [SerializeField] private Button orderButton;

    [Header("주문 확인 패널")]
    [SerializeField] private GameObject confirmPanel;
    [SerializeField] private TMP_Text confirmStockText;
    [SerializeField] private TMP_Text confirmSideText;
    [SerializeField] private TMP_Text confirmQuantityText;
    [SerializeField] private TMP_Text confirmPriceText;
    [SerializeField] private TMP_Text confirmAmountText;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button confirmButton;

    [Header("상태 / 오류 표시")]
    [Tooltip("확인 패널이 닫혀 있어도 보이는 텍스트를 연결하세요.")]
    [SerializeField]
    private TMP_Text messageText;

    [Header("주문 후 데이터 갱신 완료 이벤트 - 선택")]
    [SerializeField]
    private UnityEvent onPortfolioUpdated;

    [Header("보유현금")]
    [SerializeField]
    private TMP_Text availableCashText;

    private ScenarioRuntimeData scenario;
    private AssetInfo asset;

    private bool isBuy = true;
    private bool busy;

    // 통신 오류 시 서버에 주문이 도착했을 가능성이 있으므로
    // 자동 재주문을 막습니다.
    private bool outcomeUnknown;

    private OrderRequest pendingOrder;
    private ScenarioRuntimeData pendingScenario;
    private int pendingTurn;

    // 이번 세션 / 이번 턴의 성공 주문 목록
    private readonly List<ScenarioOrderSummary> currentTurnOrders =
        new List<ScenarioOrderSummary>();

    private string recordedSessionId;
    private int recordedTurn = -1;

    private void Awake()
    {
        if (buyButton != null)
            buyButton.onClick.AddListener(SelectBuy);

        if (sellButton != null)
            sellButton.onClick.AddListener(SelectSell);

        if (minusButton != null)
            minusButton.onClick.AddListener(DecreaseQuantity);

        if (plusButton != null)
            plusButton.onClick.AddListener(IncreaseQuantity);

        if (orderButton != null)
            orderButton.onClick.AddListener(OpenConfirmation);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(CloseConfirmation);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(ConfirmOrder);

        if (quantityInput != null)
        {
            quantityInput.contentType =
                TMP_InputField.ContentType.IntegerNumber;

            quantityInput.SetTextWithoutNotify("1");

            quantityInput.onValueChanged.AddListener(
                OnQuantityChanged
            );
        }

        if (confirmPanel != null)
            confirmPanel.SetActive(false);

        UpdateView();
    }

    // ScenarioPlayController에서 종목 선택 시 호출
    public void SetStock(
        ScenarioRuntimeData data,
        AssetInfo selectedAsset)
    {
        // 주문 처리 중에는 종목 변경 불가
        if (busy)
            return;

        scenario = data;
        asset = selectedAsset;
        pendingOrder = null;

        // 다른 시나리오 또는 다음 턴이면 주문 내역 초기화
        ResetOrdersWhenTurnChanged(data);

        if (confirmPanel != null)
            confirmPanel.SetActive(false);

        if (quantityInput != null)
            quantityInput.SetTextWithoutNotify("1");

        if (!outcomeUnknown)
            SetMessage("");

        UpdateView();
    }

    public void SelectBuy()
    {
        if (IsLocked())
            return;

        isBuy = true;
        UpdateView();
    }

    public void SelectSell()
    {
        if (IsLocked())
            return;

        isBuy = false;
        UpdateView();
    }

    public void IncreaseQuantity()
    {
        if (IsLocked())
            return;

        int quantity = ReadQuantity();
        int ownedQuantity = GetOwnedQuantity();

        if (!isBuy && quantity >= ownedQuantity)
            return;

        quantity = quantity < 1
            ? 1
            : quantity < int.MaxValue
                ? quantity + 1
                : int.MaxValue;

        if (quantityInput != null)
            quantityInput.text = quantity.ToString();
    }

    public void DecreaseQuantity()
    {
        if (IsLocked())
            return;

        if (quantityInput != null)
        {
            quantityInput.text =
                Mathf.Max(1, ReadQuantity() - 1).ToString();
        }
    }

    private void OnQuantityChanged(string value)
    {
        UpdateView();
    }

    private int ReadQuantity()
    {
        return quantityInput != null &&
               int.TryParse(
                   quantityInput.text,
                   out int quantity
               ) &&
               quantity > 0
            ? quantity
            : 0;
    }

    private bool IsLocked()
    {
        return busy ||
               outcomeUnknown ||
               pendingOrder != null;
    }

    private bool HasStock()
    {
        return scenario?.session != null &&
               scenario.turnData?.progress != null &&
               !string.IsNullOrEmpty(
                   scenario.session.session_id
               ) &&
               asset != null &&
               asset.data_available &&
               asset.current_price > 0;
    }

    private int GetOwnedQuantity()
    {
        if (scenario?.turnData?.portfolio?.positions == null ||
            asset == null)
        {
            return 0;
        }

        foreach (PositionInfo position in
                 scenario.turnData.portfolio.positions)
        {
            if (position != null &&
                position.asset_id == asset.asset_id)
            {
                return Mathf.Max(0, position.quantity);
            }
        }

        return 0;
    }

    private void UpdateView()
    {
        SetTextColor(
            buyText,
            isBuy ? selectedColor : normalColor
        );

        SetTextColor(
            sellText,
            isBuy ? normalColor : selectedColor
        );

        bool hasStock = HasStock();
        bool editable = !IsLocked();

        int quantity = ReadQuantity();
        int ownedQuantity = GetOwnedQuantity();

        SetText(
            priceText,
            hasStock
                ? $"{asset.current_price:N0}원"
                : "-"
        );

        SetText(
            estimatedAmountText,
            hasStock && quantity > 0
                ? $"{(decimal)asset.current_price * quantity:N0}원"
                : "-"
        );

        bool canSell =
            !isBuy &&
            ownedQuantity > 0 &&
            quantity > 0 &&
            quantity <= ownedQuantity;

        decimal estimatedAmount =
    hasStock && quantity > 0
        ? (decimal)asset.current_price * quantity
        : 0m;

        bool hasEnoughCash =
            scenario?.turnData?.portfolio != null &&
            estimatedAmount <= scenario.turnData.portfolio.cash;

        bool canOrder =
            hasStock &&
            quantity > 0 &&
            (
                isBuy
                    ? hasEnoughCash
                    : canSell
            );

        if (buyButton != null)
            buyButton.interactable = editable;

        if (sellButton != null)
            sellButton.interactable = editable;

        if (quantityInput != null)
            quantityInput.interactable = editable;

        if (minusButton != null)
        {
            minusButton.interactable =
                editable && quantity > 1;
        }

        if (plusButton != null)
        {
            plusButton.interactable =
                editable &&
                quantity < int.MaxValue &&
                (isBuy || quantity < ownedQuantity);
        }

        if (orderButton != null)
            orderButton.interactable = editable && canOrder;

        if (confirmButton != null)
        {
            confirmButton.interactable =
                pendingOrder != null &&
                !busy &&
                !outcomeUnknown;
        }

        if (cancelButton != null)
            cancelButton.interactable = !busy;

        PortfolioInfo portfolio =
    scenario?.turnData?.portfolio;

        SetText(
            availableCashText,
            portfolio != null
                ? portfolio.cash.ToString("N0") + "원"
                : "-"
        );
    }

    public void OpenConfirmation()
    {
        if (IsLocked())
            return;

        int quantity = ReadQuantity();

        if (!HasStock() || quantity < 1)
        {
            SetMessage("종목과 주문 수량을 확인하세요.");
            return;
        }

        if (!isBuy && quantity > GetOwnedQuantity())
        {
            SetMessage("보유 수량보다 많이 매도할 수 없습니다.");
            return;
        }

        decimal orderAmount =
    (decimal)asset.current_price * quantity;

        if (isBuy &&
            (
                scenario.turnData.portfolio == null ||
                orderAmount > scenario.turnData.portfolio.cash
            ))
        {
            SetMessage("보유현금이 부족합니다.");
            return;
        }

        pendingScenario = scenario;
        pendingTurn = scenario.turnData.progress.current_turn;

        pendingOrder = new OrderRequest
        {
            asset_id = asset.asset_id,
            side = isBuy ? "BUY" : "SELL",
            quantity = quantity
        };

        SetText(
            confirmStockText,
            $"{asset.name} ({asset.asset_id})"
        );

        SetText(
            confirmSideText,
            isBuy ? "매수" : "매도"
        );

        SetText(
            confirmQuantityText,
            $"{pendingOrder.quantity:N0}주"
        );

        SetText(
            confirmPriceText,
            $"{asset.current_price:N0}원"
        );

        SetText(
            confirmAmountText,
            $"{(decimal)asset.current_price * pendingOrder.quantity:N0}원"
        );

        SetMessage("");

        if (confirmPanel != null)
            confirmPanel.SetActive(true);

        UpdateView();
    }

    public void CloseConfirmation()
    {
        if (busy)
            return;

        pendingOrder = null;

        if (confirmPanel != null)
            confirmPanel.SetActive(false);

        UpdateView();
    }

    public void ConfirmOrder()
    {
        if (busy ||
            outcomeUnknown ||
            pendingOrder == null)
        {
            return;
        }

        if (pendingScenario?.turnData?.progress == null ||
            pendingScenario.turnData.progress.current_turn !=
            pendingTurn)
        {
            CloseConfirmation();

            SetMessage(
                "턴이 변경되었습니다. 주문 내용을 다시 확인하세요."
            );

            return;
        }

        busy = true;
        UpdateView();

        StartCoroutine(
            SendOrder(
                pendingScenario,
                pendingOrder
            )
        );
    }

    private IEnumerator SendOrder(
        ScenarioRuntimeData target,
        OrderRequest body)
    {
        string sessionPath =
            ServerConfig.HttpBaseUrl +
            "/api/sessions/" +
            UnityWebRequest.EscapeURL(
                target.session.session_id
            );

        SetMessage("주문 처리 중...");

        using (UnityWebRequest request =
               new UnityWebRequest(
                   sessionPath + "/orders",
                   UnityWebRequest.kHttpVerbPOST
               ))
        {
            request.uploadHandler =
                new UploadHandlerRaw(
                    Encoding.UTF8.GetBytes(
                        JsonUtility.ToJson(body)
                    )
                );

            request.downloadHandler =
                new DownloadHandlerBuffer();

            request.timeout = 20;

            request.SetRequestHeader(
                "Content-Type",
                "application/json"
            );

            request.SetRequestHeader(
                "Accept",
                "application/json"
            );

            yield return request.SendWebRequest();

            string raw =
                request.downloadHandler != null
                    ? request.downloadHandler.text
                    : "";

            OrderResponse response =
                Parse<OrderResponse>(raw);

            bool success =
                request.result ==
                UnityWebRequest.Result.Success &&
                response?.status == "ok";

            if (!success)
            {
                bool rejected =
                    request.responseCode >= 400 &&
                    request.responseCode < 500;

                outcomeUnknown = !rejected;

                string error = rejected
                    ? "주문 거절: " +
                      (response?.message ??
                       "주문 정보를 확인하세요.")
                    : "주문 처리 여부를 확인할 수 없습니다. " +
                      "중복 주문 방지를 위해 추가 주문을 잠갔습니다.";

                Debug.LogError(
                    $"[Scenario Order] HTTP {request.responseCode}\n{raw}"
                );

                Finish(error);
                yield break;
            }

            Debug.Log(
                $"[Scenario Order] 주문 성공\n{raw}"
            );

            // 서버가 성공 처리한 주문만 턴 종료 목록에 기록
            RecordCompletedOrder(target, body);
        }

        // 주문 뒤 최신 현금 / 보유 종목 갱신
        using (UnityWebRequest request =
               UnityWebRequest.Get(sessionPath + "/turn"))
        {
            request.timeout = 15;

            yield return request.SendWebRequest();

            CurrentTurnResponse response =
                Parse<CurrentTurnResponse>(
                    request.downloadHandler.text
                );

            if (request.result !=
                    UnityWebRequest.Result.Success ||
                response?.status != "ok" ||
                response.data == null)
            {
                Finish(
                    "주문은 성공했지만 잔고 갱신에 실패했습니다. " +
                    "같은 주문을 다시 보내지 마세요."
                );

                yield break;
            }

            target.turnData = response.data;

            if (response.data.session != null)
                target.session = response.data.session;

            Finish("주문이 처리되었습니다.");
            onPortfolioUpdated?.Invoke();
        }
    }

    private void ResetOrdersWhenTurnChanged(
        ScenarioRuntimeData target)
    {
        if (target?.session == null ||
            target.turnData?.progress == null)
        {
            return;
        }

        string sessionId = target.session.session_id;
        int turnNo = target.turnData.progress.current_turn;

        // 새 시나리오를 시작하거나 다음 턴으로 넘어간 경우에만 초기화
        if (recordedSessionId != sessionId ||
            recordedTurn != turnNo)
        {
            currentTurnOrders.Clear();

            recordedSessionId = sessionId;
            recordedTurn = turnNo;
        }
    }

    private void RecordCompletedOrder(
        ScenarioRuntimeData target,
        OrderRequest body)
    {
        if (target?.session == null ||
            target.turnData?.progress == null ||
            asset == null)
        {
            return;
        }

        ResetOrdersWhenTurnChanged(target);

        ScenarioOrderSummary order =
            new ScenarioOrderSummary
            {
                asset_id = body.asset_id,
                asset_name = asset.name,
                side = body.side,
                quantity = body.quantity,
                price = asset.current_price
            };

        currentTurnOrders.Add(order);

        OnOrderCompleted?.Invoke(order);
    }

    private void Finish(string message)
    {
        busy = false;
        pendingOrder = null;

        if (confirmPanel != null)
            confirmPanel.SetActive(false);

        SetMessage(message);
        UpdateView();
    }

    private static T Parse<T>(string json)
        where T : class
    {
        try
        {
            return JsonUtility.FromJson<T>(json);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void SetMessage(string value)
    {
        SetText(messageText, value);
    }

    private static void SetText(
        TMP_Text text,
        string value)
    {
        if (text != null)
            text.text = value;
    }

    private static void SetTextColor(
        TMP_Text text,
        Color color)
    {
        if (text != null)
            text.color = color;
    }

    private void OnDestroy()
    {
        if (buyButton != null)
            buyButton.onClick.RemoveListener(SelectBuy);

        if (sellButton != null)
            sellButton.onClick.RemoveListener(SelectSell);

        if (minusButton != null)
            minusButton.onClick.RemoveListener(DecreaseQuantity);

        if (plusButton != null)
            plusButton.onClick.RemoveListener(IncreaseQuantity);

        if (orderButton != null)
            orderButton.onClick.RemoveListener(OpenConfirmation);

        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(CloseConfirmation);

        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(ConfirmOrder);

        if (quantityInput != null)
        {
            quantityInput.onValueChanged.RemoveListener(
                OnQuantityChanged
            );
        }
    }

    [Serializable]
    private class OrderRequest
    {
        public string asset_id;
        public string side;
        public int quantity;
    }

    [Serializable]
    private class OrderResponse
    {
        public string status;
        public string message;
        public string error_code;
    }
}

[Serializable]
public class ScenarioOrderSummary
{
    public string asset_id;
    public string asset_name;
    public string side;
    public int quantity;
    public long price;

    public long Amount => price * (long)quantity;
}

