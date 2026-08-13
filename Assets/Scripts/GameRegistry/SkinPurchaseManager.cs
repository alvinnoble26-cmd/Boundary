using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing;
using Unity.Services.Core;

public sealed class SkinPurchaseManager : MonoBehaviour
{
    public const string SunDuckerProductId = "com.alvin.entropy.skin.sunducker";
    public const string TurtleProductId = "com.alvin.entropy.skin.turtle";
    public static SkinPurchaseManager I { get; private set; }
    public event Action Changed;
    public string SunDuckerDisplayPrice { get; private set; } = "$4.99";
    public string TurtleDisplayPrice { get; private set; } = "$0.29";
    public string DisplayPrice => SunDuckerDisplayPrice;
    public bool StoreReady => controller != null &&
                              controller.GetProductById(SunDuckerProductId) != null &&
                              controller.GetProductById(TurtleProductId) != null;
    public bool PurchaseBusy { get; private set; }
    public string StatusMessage { get; private set; } = "";

    private StoreController controller;
    private string requestedProductId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        if (I != null) return;
        DontDestroyOnLoad(new GameObject("SkinPurchaseManager").AddComponent<SkinPurchaseManager>());
    }

    private async void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        if (Application.isBatchMode ||
            SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            Destroy(gameObject);
            return;
        }

        try
        {
            await UnityServices.InitializeAsync();

            controller = UnityIAPServices.StoreController();
            controller.OnStoreDisconnected += OnStoreDisconnected;
            controller.OnProductsFetched += OnProductsFetched;
            controller.OnProductsFetchFailed += OnProductsFetchFailed;
            controller.OnPurchasesFetched += OnPurchasesFetched;
            controller.OnPurchasesFetchFailed += OnPurchasesFetchFailed;
            controller.OnPurchasePending += OnPurchasePending;
            controller.OnPurchaseFailed += OnPurchaseFailed;
            controller.OnPurchaseDeferred += OnPurchaseDeferred;

            await controller.Connect();
            var catalog = new CatalogProvider();
            catalog.AddProduct(SunDuckerProductId, ProductType.NonConsumable);
            catalog.AddProduct(TurtleProductId, ProductType.NonConsumable);
            catalog.FetchProducts(products => controller.FetchProducts(products));
        }
        catch (Exception e)
        {
            requestedProductId = null;
            StatusMessage = "APP STORE INITIALIZATION FAILED — TRY AGAIN";
            Debug.LogWarning("[SkinShop] Store initialization failed: " + e.Message);
            Changed?.Invoke();
        }
    }

    private void OnDestroy()
    {
        if (controller == null) return;
        controller.OnStoreDisconnected -= OnStoreDisconnected;
        controller.OnProductsFetched -= OnProductsFetched;
        controller.OnProductsFetchFailed -= OnProductsFetchFailed;
        controller.OnPurchasesFetched -= OnPurchasesFetched;
        controller.OnPurchasesFetchFailed -= OnPurchasesFetchFailed;
        controller.OnPurchasePending -= OnPurchasePending;
        controller.OnPurchaseFailed -= OnPurchaseFailed;
        controller.OnPurchaseDeferred -= OnPurchaseDeferred;
    }

    public void PurchaseSunDucker()
    {
        PurchaseSkin(SunDuckerProductId);
    }

    public void PurchaseTurtle()
    {
        PurchaseSkin(TurtleProductId);
    }

    private void PurchaseSkin(string productId)
    {
        if (PurchaseBusy) return;
#if UNITY_IOS && !UNITY_EDITOR
        requestedProductId = productId;
        StatusMessage = "CONNECTING TO APP STORE...";
        Changed?.Invoke();

        Product product = controller?.GetProductById(productId);
        if (product == null || !product.availableToPurchase)
        {
            // StoreKit can still be fetching its catalog when the shop first opens.
            // Keep this purchase request queued and explicitly retry the catalog fetch.
            if (controller != null)
            {
                var catalog = new CatalogProvider();
                catalog.AddProduct(SunDuckerProductId, ProductType.NonConsumable);
                catalog.AddProduct(TurtleProductId, ProductType.NonConsumable);
                catalog.FetchProducts(products => controller.FetchProducts(products));
            }
            else
            {
                StatusMessage = "APP STORE IS STILL LOADING — TAP AGAIN";
                Changed?.Invoke();
            }
            return;
        }
        BeginPurchase(product);
#else
        Debug.LogWarning("[SkinShop] Apple purchases can only be tested in an iOS build " +
                         "with a sandbox/TestFlight Apple account.");
#endif
    }

    private void BeginPurchase(Product product)
    {
        if (PurchaseBusy || product == null || !product.availableToPurchase) return;
        requestedProductId = null;
        PurchaseBusy = true;
        StatusMessage = "PURCHASING...";
        SfxManager.PlaySkinPurchase();
        Changed?.Invoke();
        controller.PurchaseProduct(product);
    }

    public void RestorePurchases()
    {
#if UNITY_IOS && !UNITY_EDITOR
        if (controller == null)
        {
            StatusMessage = "APP STORE IS STILL LOADING — TRY AGAIN";
            Changed?.Invoke();
            return;
        }
        StatusMessage = "RECOVERING PURCHASES...";
        Changed?.Invoke();
        controller.RestoreTransactions((success, error) =>
        {
            Debug.Log("[SkinShop] Recover owned skins: " + success + " " + error);
            StatusMessage = success ? "CHECKING OWNED SKINS..." : "RECOVERY FAILED — TRY AGAIN";
            Changed?.Invoke();
            if (success) controller.FetchPurchases();
        });
#else
        Debug.LogWarning("[SkinShop] Recover Owned Skins is available in the iOS build.");
#endif
    }

    private void OnProductsFetched(List<Product> products)
    {
        Product sunProduct = products.FirstOrDefault(p => p.definition.id == SunDuckerProductId);
        Product turtleProduct = products.FirstOrDefault(p => p.definition.id == TurtleProductId);
        if (Application.platform == RuntimePlatform.IPhonePlayer)
        {
            if (sunProduct?.metadata != null &&
                !string.IsNullOrWhiteSpace(sunProduct.metadata.localizedPriceString))
                SunDuckerDisplayPrice = sunProduct.metadata.localizedPriceString;
            if (turtleProduct?.metadata != null &&
                !string.IsNullOrWhiteSpace(turtleProduct.metadata.localizedPriceString))
                TurtleDisplayPrice = turtleProduct.metadata.localizedPriceString;
        }
        controller.FetchPurchases();
        StatusMessage = "";
        Changed?.Invoke();
        if (!string.IsNullOrEmpty(requestedProductId))
        {
            Product requested = controller.GetProductById(requestedProductId);
            if (requested != null && requested.availableToPurchase)
                BeginPurchase(requested);
        }
    }

    private void OnProductsFetchFailed(ProductFetchFailed failure)
    {
        requestedProductId = null;
        StatusMessage = "UNAVAILABLE — CHECK APP STORE SETUP";
        Debug.LogWarning("[SkinShop] Product fetch failed: " + failure);
        Changed?.Invoke();
    }

    private void OnStoreDisconnected(StoreConnectionFailureDescription failure)
    {
        requestedProductId = null;
        StatusMessage = "APP STORE CONNECTION FAILED — TRY AGAIN";
        Debug.LogWarning("[SkinShop] Store disconnected: " + failure);
        Changed?.Invoke();
    }

    private void OnPurchasesFetchFailed(PurchasesFetchFailureDescription failure) =>
        Debug.LogWarning("[SkinShop] Purchase recovery fetch failed: " + failure);

    private void OnPurchasesFetched(Orders orders)
    {
        StatusMessage = "";
        foreach (ConfirmedOrder order in orders.ConfirmedOrders)
        {
            if (GetSupportedProductId(order) != null) VerifyRestoredOrderAsync(order);
        }
    }

    private void OnPurchasePending(PendingOrder order)
    {
        if (GetSupportedProductId(order) != null) VerifyAndFinishAsync(order);
        else controller.ConfirmPurchase(order);
    }

    private async void VerifyAndFinishAsync(PendingOrder order)
    {
        string productId = GetSupportedProductId(order);
        try
        {
            bool granted = productId != null && FirebaseManager.I != null &&
                           await FirebaseManager.I.VerifyAppleSkinPurchaseAsync(
                               order.Info.Receipt, productId);
            if (granted)
            {
                controller.ConfirmPurchase(order);
                StatusMessage = "OWNED";
                Debug.Log("[SkinShop] Purchase verified and saved: " + productId);
            }
            else
            {
                StatusMessage = "PURCHASE NEEDS RECOVERY";
                Debug.LogError("[SkinShop] The transaction completed, but secure verification " +
                               "did not finish. Recover Owned Skins will retry it.");
            }
        }
        catch (Exception e)
        {
            StatusMessage = "PURCHASE NEEDS RECOVERY";
            Debug.LogError("[SkinShop] Secure purchase verification failed: " + e.Message +
                           ". Recover Owned Skins will retry it.");
        }
        finally
        {
            PurchaseBusy = false;
            Changed?.Invoke();
        }
    }

    private async void VerifyRestoredOrderAsync(ConfirmedOrder order)
    {
        if (FirebaseManager.I == null) return;
        string productId = GetSupportedProductId(order);
        if (productId == null) return;
        try
        {
            await FirebaseManager.I.VerifyAppleSkinPurchaseAsync(order.Info.Receipt, productId);
        }
        catch (Exception e)
        {
            Debug.LogError("[SkinShop] Restored purchase verification failed: " + e.Message);
        }
        finally
        {
            Changed?.Invoke();
        }
    }

    private void OnPurchaseFailed(FailedOrder order)
    {
        PurchaseBusy = false;
        requestedProductId = null;
        StatusMessage = "PURCHASE CANCELLED OR FAILED — TRY AGAIN";
        Debug.LogWarning("[SkinShop] Purchase failed: " + order.FailureReason + " " + order.Details);
        Changed?.Invoke();
    }

    private void OnPurchaseDeferred(DeferredOrder order)
    {
        PurchaseBusy = false;
        requestedProductId = null;
        StatusMessage = "WAITING FOR PURCHASE APPROVAL";
        Debug.Log("[SkinShop] Purchase is waiting for approval.");
        Changed?.Invoke();
    }

    private static string GetSupportedProductId(Order order)
    {
        if (order?.CartOrdered?.Items() == null) return null;
        foreach (var item in order.CartOrdered.Items())
        {
            string productId = item?.Product?.definition?.id;
            if (productId == SunDuckerProductId || productId == TurtleProductId)
                return productId;
        }
        return null;
    }
}
