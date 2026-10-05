// Buys a permanent (non-consumable) product. PluginYG2's BuyPayments always calls
// consumePurchase after a successful payment, which would delete a permanent product from
// getPurchases() and leave the game unable to restore it on another device. This path pays
// and reports success through the same YG2Instance callbacks, but never consumes.
// `payments`, `YG2Instance` and `FocusGame` are top-level bindings of the YandexGames
// template (index.html + the Payments module code the plugin injects into it).
mergeInto(LibraryManager.library, {

    BP_BuyPermanent: function (idPtr) {
        var id = UTF8ToString(idPtr);

        function done(method) {
            if (typeof YG2Instance === "function") {
                YG2Instance(method, id);
            }
            if (typeof FocusGame === "function") {
                FocusGame();
            }
        }

        try {
            if (typeof payments === "undefined" || payments == null) {
                console.warn("[BlockPuzzle] Permanent purchase: payments are not ready");
                done("OnPurchaseFailed");
                return;
            }

            payments.purchase({ id: id }).then(function () {
                done("OnPurchaseSuccess");
            }).catch(function (e) {
                console.error("[BlockPuzzle] Permanent purchase failed", e && e.message);
                done("OnPurchaseFailed");
            });
        } catch (e) {
            console.error("[BlockPuzzle] Permanent purchase crashed", e && e.message);
            done("OnPurchaseFailed");
        }
    }
});
