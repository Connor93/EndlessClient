using System;
using EndlessClient.Dialogs.Factories;
using EndlessClient.HUD;
using EndlessClient.UI.Myra;
using EOLib.Domain.Character;
using EOLib.Domain.Interact.Bank;
using EOLib.IO.Repositories;
using EOLib.Localization;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Optional.Collections;
using XNAControls;

namespace EndlessClient.Dialogs
{
    /// <summary>
    /// Native Myra replacement for the XNA BankAccountDialog.
    /// Displays deposit/withdraw/upgrade options with current bank balance in the title.
    /// </summary>
    public class MyraBankAccountDialog : MyraScrollingListDialog
    {
        private readonly IBankActions _bankActions;
        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly IStatusLabelSetter _statusLabelSetter;
        private readonly IEOMessageBoxFactory _messageBoxFactory;
        private readonly IItemTransferDialogFactory _itemTransferDialogFactory;
        private readonly IBankDataProvider _bankDataProvider;
        private readonly ICharacterInventoryProvider _characterInventoryProvider;
        private readonly IEIFFileProvider _eifFileProvider;

        private int _cachedValue;
        private int _cachedUpgrades = -1;

        public MyraBankAccountDialog(
            IMyraUIManager uiManager,
            IMyraFontProvider fontProvider,
            IBankActions bankActions,
            ILocalizedStringFinder localizedStringFinder,
            IStatusLabelSetter statusLabelSetter,
            IEOMessageBoxFactory messageBoxFactory,
            IItemTransferDialogFactory itemTransferDialogFactory,
            IBankDataProvider bankDataProvider,
            ICharacterInventoryProvider characterInventoryProvider,
            IEIFFileProvider eifFileProvider)
            : base(uiManager, fontProvider, $"Bank Account  ({bankDataProvider.AccountValue} gold)", width: 320, height: 280)
        {
            _bankActions = bankActions;
            _localizedStringFinder = localizedStringFinder;
            _statusLabelSetter = statusLabelSetter;
            _messageBoxFactory = messageBoxFactory;
            _itemTransferDialogFactory = itemTransferDialogFactory;
            _bankDataProvider = bankDataProvider;
            _characterInventoryProvider = characterInventoryProvider;
            _eifFileProvider = eifFileProvider;

            _cachedValue = bankDataProvider.AccountValue;

            var currencyName = _eifFileProvider.EIFFile[1].Name;

            // Deposit
            var depositText = _localizedStringFinder.GetString(EOResourceID.DIALOG_BANK_DEPOSIT);
            var depositSub = $"{_localizedStringFinder.GetString(EOResourceID.DIALOG_BANK_TRANSFER)} {currencyName} {_localizedStringFinder.GetString(EOResourceID.DIALOG_BANK_TO_ACCOUNT)}";
            AddItem(depositText, depositSub, onClick: _ => Deposit(), isLink: true);

            // Withdraw
            var withdrawText = _localizedStringFinder.GetString(EOResourceID.DIALOG_BANK_WITHDRAW);
            var withdrawSub = $"{_localizedStringFinder.GetString(EOResourceID.DIALOG_BANK_TRANSFER)} {currencyName} {_localizedStringFinder.GetString(EOResourceID.DIALOG_BANK_FROM_ACCOUNT)}";
            AddItem(withdrawText, withdrawSub, onClick: _ => Withdraw(), isLink: true);

            // Locker Upgrade
            var upgradeText = _localizedStringFinder.GetString(EOResourceID.DIALOG_BANK_LOCKER_UPGRADE);
            var upgradeSub = _localizedStringFinder.GetString(EOResourceID.DIALOG_BANK_MORE_SPACE);
            AddItem(upgradeText, upgradeSub, onClick: _ => Upgrade(), isLink: true);

            SetupButtons(showOk: false, showCancel: true);
        }

        public override void Update(GameTime gameTime)
        {
            // Poll for balance changes (server updates BankDataRepository asynchronously)
            if (_bankDataProvider.AccountValue != _cachedValue)
            {
                _cachedValue = _bankDataProvider.AccountValue;
                Window.Title = $"Bank Account  ({_cachedValue} gold)";
            }

            // Poll for locker upgrade changes
            _bankDataProvider.LockerUpgrades.MatchSome(upgrades =>
            {
                if (upgrades != _cachedUpgrades)
                {
                    if (_cachedUpgrades >= 0)
                    {
                        _statusLabelSetter.SetStatusLabel(
                            EOResourceID.STATUS_LABEL_TYPE_INFORMATION,
                            EOResourceID.STATUS_LABEL_LOCKER_SPACE_INCREASED);
                    }
                    _cachedUpgrades = upgrades;
                }
            });

            base.Update(gameTime);
        }

        private void Deposit()
        {
            _characterInventoryProvider.ItemInventory.SingleOrNone(x => x.ItemID == 1)
                .Match(
                    some: characterGold =>
                    {
                        if (characterGold.Amount == 0)
                        {
                            var dlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.BANK_ACCOUNT_UNABLE_TO_DEPOSIT);
                            dlg.ShowDialog();
                        }
                        else if (characterGold.Amount == 1)
                        {
                            _bankActions.Deposit(1);
                        }
                        else if (characterGold.Amount > 1)
                        {
                            var dlg = _itemTransferDialogFactory.CreateItemTransferDialog(
                                _eifFileProvider.EIFFile[1].Name,
                                ItemTransferType.BankTransfer,
                                characterGold.Amount,
                                EOResourceID.DIALOG_TRANSFER_DEPOSIT);
                            dlg.DialogClosing += (_, e) =>
                            {
                                if (e.Result == XNADialogResult.OK)
                                    _bankActions.Deposit(dlg.SelectedAmount);
                            };
                            dlg.ShowDialog();
                        }
                    },
                    none: () =>
                    {
                        var dlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.BANK_ACCOUNT_UNABLE_TO_DEPOSIT);
                        dlg.ShowDialog();
                    });
        }

        private void Withdraw()
        {
            if (_bankDataProvider.AccountValue == 0)
            {
                var dlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.BANK_ACCOUNT_UNABLE_TO_WITHDRAW);
                dlg.ShowDialog();
            }
            else if (_bankDataProvider.AccountValue == 1)
            {
                _bankActions.Withdraw(1);
            }
            else if (_bankDataProvider.AccountValue > 1)
            {
                var dlg = _itemTransferDialogFactory.CreateItemTransferDialog(
                    _eifFileProvider.EIFFile[1].Name,
                    ItemTransferType.BankTransfer,
                    _bankDataProvider.AccountValue,
                    EOResourceID.DIALOG_TRANSFER_WITHDRAW);
                dlg.DialogClosing += (_, e) =>
                {
                    if (e.Result == XNADialogResult.OK)
                        _bankActions.Withdraw(dlg.SelectedAmount);
                };
                dlg.ShowDialog();
            }
        }

        private void Upgrade()
        {
            _bankDataProvider.LockerUpgrades.MatchSome(lockerUpgrades =>
            {
                if (lockerUpgrades == Constants.MaxLockerUpgrades)
                {
                    var dlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.LOCKER_UPGRADE_IMPOSSIBLE);
                    dlg.ShowDialog();
                    return;
                }

                int requiredGold = (lockerUpgrades + 1) * 1000;

                _characterInventoryProvider.ItemInventory.SingleOrNone(x => x.ItemID == 1)
                    .Match(
                        some: characterGold =>
                        {
                            if (characterGold.Amount < requiredGold)
                            {
                                var dlg = _messageBoxFactory.CreateMessageBox(
                                    DialogResourceID.WARNING_YOU_HAVE_NOT_ENOUGH,
                                    $" {_eifFileProvider.EIFFile[1].Name}");
                                dlg.ShowDialog();
                            }
                            else
                            {
                                var dlg = _messageBoxFactory.CreateMessageBox(
                                    DialogResourceID.LOCKER_UPGRADE_UNIT,
                                    $"{requiredGold} {_eifFileProvider.EIFFile[1].Name}?",
                                    EODialogButtons.OkCancel);
                                dlg.DialogClosing += (_, e) =>
                                {
                                    if (e.Result == XNADialogResult.OK)
                                        _bankActions.BuyStorageUpgrade();
                                };
                                dlg.ShowDialog();
                            }
                        },
                        () =>
                        {
                            var dlg = _messageBoxFactory.CreateMessageBox(
                                DialogResourceID.WARNING_YOU_HAVE_NOT_ENOUGH,
                                $" {_eifFileProvider.EIFFile[1].Name}");
                            dlg.ShowDialog();
                        });
            });
        }
    }
}
