using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
    // A transaction status belongs to one storefront item. Keeping this with
    // the product prevents a Turtle purchase from changing the Sun Ducker card.
    private string statusProductId;

    public string StatusForProduct(string productId)
    {
        return statusProductId == productId ? StatusMessage : "";
    }

    private void SetStatus(string message, string productId = null)
    {
        StatusMessage = message;
        statusProductId = productId;
    }

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
            SetStatus("APP STORE INITIALIZATION FAILED — TRY AGAIN");
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
        SetStatus("CONNECTING TO APP STORE...", productId);
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
                SetStatus("APP STORE IS STILL LOADING — TAP AGAIN", productId);
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
        SetStatus("PURCHASING...", product.definition.id);
        SfxManager.PlaySkinPurchase();
        Changed?.Invoke();
        controller.PurchaseProduct(product);
    }

    public void RestorePurchases()
    {
#if UNITY_IOS && !UNITY_EDITOR
        if (controller == null)
        {
            SetStatus("APP STORE IS STILL LOADING — TRY AGAIN");
            Changed?.Invoke();
            return;
        }
        SetStatus("RECOVERING PURCHASES...");
        Changed?.Invoke();
        controller.RestoreTransactions((success, error) =>
        {
            Debug.Log("[SkinShop] Recover owned skins: " + success + " " + error);
            SetStatus(success ? "CHECKING OWNED SKINS..." : "RECOVERY FAILED — TRY AGAIN");
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
        SetStatus("");
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
        string failedProductId = requestedProductId;
        requestedProductId = null;
        SetStatus("UNAVAILABLE — CHECK APP STORE SETUP", failedProductId);
        Debug.LogWarning("[SkinShop] Product fetch failed: " + failure);
        Changed?.Invoke();
    }

    private void OnStoreDisconnected(StoreConnectionFailureDescription failure)
    {
        string failedProductId = requestedProductId;
        requestedProductId = null;
        SetStatus("APP STORE CONNECTION FAILED — TRY AGAIN", failedProductId);
        Debug.LogWarning("[SkinShop] Store disconnected: " + failure);
        Changed?.Invoke();
    }

    private void OnPurchasesFetchFailed(PurchasesFetchFailureDescription failure)
    {
        SetStatus("RECOVERY FAILED — TRY AGAIN");
        Debug.LogWarning("[SkinShop] Purchase recovery fetch failed: " + failure);
        Changed?.Invoke();
    }

    private void OnPurchasesFetched(Orders orders)
    {
        SetStatus("");
        Changed?.Invoke();
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
            FirebaseManager firebase = await WaitForFirebaseAsync();
            bool granted = productId != null && firebase != null &&
                           await firebase.VerifyAppleSkinPurchaseAsync(
                               order.Info.Receipt, productId, order.Info.Apple?.jwsRepresentation);
            if (granted)
            {
                controller.ConfirmPurchase(order);
                SetStatus("OWNED", productId);
                Debug.Log("[SkinShop] Purchase verified and saved: " + productId);
            }
            else
            {
                SetStatus("PURCHASE NEEDS RECOVERY", productId);
                Debug.LogError("[SkinShop] The transaction completed, but secure verification " +
                               "did not finish. Recover Owned Skins will retry it.");
            }
        }
        catch (Exception e)
        {
            SetStatus("PURCHASE NEEDS RECOVERY", productId);
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
        string productId = GetSupportedProductId(order);
        if (productId == null) return;
        try
        {
            FirebaseManager firebase = await WaitForFirebaseAsync();
            bool granted = firebase != null && await firebase.VerifyAppleSkinPurchaseAsync(
                order.Info.Receipt, productId, order.Info.Apple?.jwsRepresentation);
            SetStatus(granted ? "OWNED" : "PURCHASE NEEDS RECOVERY", productId);
            if (!granted)
                Debug.LogWarning("[SkinShop] Restored transaction could not be verified for " + productId);
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

    // StoreKit may report restored transactions before the Firebase component
    // in the first scene has completed anonymous sign-in. Do not discard those
    // orders; wait briefly for the authenticated entitlement service instead.
    private static async Task<FirebaseManager> WaitForFirebaseAsync()
    {
        const int maxFrames = 900; // about 15 seconds at 60 fps
        for (int frame = 0; frame < maxFrames; frame++)
        {
            FirebaseManager firebase = FirebaseManager.I;
            if (firebase != null && firebase.IsReady) return firebase;
            await Task.Yield();
        }

        Debug.LogWarning("[SkinShop] Firebase was not ready to verify an Apple transaction.");
        return null;
    }

    private void OnPurchaseFailed(FailedOrder order)
    {
        PurchaseBusy = false;
        requestedProductId = null;
        SetStatus("PURCHASE CANCELLED OR FAILED — TRY AGAIN", GetSupportedProductId(order));
        Debug.LogWarning("[SkinShop] Purchase failed: " + order.FailureReason + " " + order.Details);
        Changed?.Invoke();
    }

    private void OnPurchaseDeferred(DeferredOrder order)
    {
        PurchaseBusy = false;
        requestedProductId = null;
        SetStatus("WAITING FOR PURCHASE APPROVAL", GetSupportedProductId(order));
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
