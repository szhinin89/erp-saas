import { LoadingState, NoAccessPage } from '../../../components/PageShell';
import { ErpPageTemplate } from '../../../templates/ErpPageTemplate';
import { CreateJournalEntryModal } from '../../../components/CreateJournalEntryModal';
import { ZHBtn } from '../../../components/zh/ZHForm';
import { AccountingAccountsTab } from '../components/AccountingAccountsTab';
import { AccountingBalanceTab } from '../components/AccountingBalanceTab';
import { AccountingConfigTab } from '../components/AccountingConfigTab';
import { AccountingJournalTab } from '../components/AccountingJournalTab';
import { AccountingMayorTab } from '../components/AccountingMayorTab';
import { useAccountingPage } from '../hooks/useAccountingPage';
import './AccountingPage.css';

export function AccountingPage() {
  const ctx = useAccountingPage();
  const {
    t,
    skipPermissionHydrationWait,
    permsHydrated,
    canUseModule,
    canViewAccounts,
    canCreateAccount,
    canViewJournal,
    canCreateJournal,
    canViewConfig,
    canEditConfig,
    setTab,
    displayTab,
    tabTitle,
    showJournal,
    setShowJournal,
    accounts,
    journalEntries,
    formRef,
    formLoading,
    activeAccountSubTab,
    configSaving,
    saveConfig,
    fetchBalance,
  } = ctx;

  if (!permsHydrated && !skipPermissionHydrationWait) {
    return (
      <ErpPageTemplate kicker={t('app.nav.group.accounting')} title={t('app.nav.accounting')}>
        <div className="acc-page-loading">
          <LoadingState />
        </div>
      </ErpPageTemplate>
    );
  }

  if (!canUseModule) return <NoAccessPage title={t('app.nav.accounting')} />;

  const headerAction = (
    <>
      {displayTab === 'accounts' && activeAccountSubTab === 'data' && canCreateAccount ? (
        <ZHBtn
          variant="primary"
          size="md"
          type="button"
          disabled={formLoading}
          onClick={() => formRef.current?.requestSubmit()}
        >
          {formLoading ? t('common.saving') : t('finance.accounts.modal.create.submit')}
        </ZHBtn>
      ) : null}
      {displayTab === 'journal' && canCreateJournal ? (
        <ZHBtn variant="primary" size="md" type="button" onClick={() => setShowJournal(true)}>
          {t('finance.journal.primaryCreate')}
        </ZHBtn>
      ) : null}
      {displayTab === 'config' && canEditConfig ? (
        <ZHBtn variant="primary" size="md" type="button" disabled={configSaving} onClick={() => void saveConfig()}>
          {configSaving ? t('common.saving') : t('common.save')}
        </ZHBtn>
      ) : null}
    </>
  );

  return (
    <ErpPageTemplate
      kicker={t('app.nav.group.accounting')}
      title={tabTitle}
      action={headerAction}
    >
      {showJournal && canCreateJournal && (
        <CreateJournalEntryModal
          accounts={accounts.data ?? []}
          onClose={() => setShowJournal(false)}
          onCreated={journalEntries.refetch}
        />
      )}

      <div className="zh-form-tabs acc-page-tabs" role="tablist">
        {canViewAccounts && (
          <button
            type="button"
            role="tab"
            aria-selected={displayTab === 'accounts'}
            className={displayTab === 'accounts' ? 'is-active' : ''}
            onClick={() => setTab('accounts')}
          >
            {t('accounting.tabs.accounts')}
          </button>
        )}
        {canViewJournal && (
          <button
            type="button"
            role="tab"
            aria-selected={displayTab === 'journal'}
            className={displayTab === 'journal' ? 'is-active' : ''}
            onClick={() => setTab('journal')}
          >
            {t('accounting.tabs.journal')}
          </button>
        )}
        {canViewJournal && (
          <button
            type="button"
            role="tab"
            aria-selected={displayTab === 'mayor'}
            className={displayTab === 'mayor' ? 'is-active' : ''}
            onClick={() => setTab('mayor')}
          >
            Mayor General
          </button>
        )}
        {canViewJournal && (
          <button
            type="button"
            role="tab"
            aria-selected={displayTab === 'balance'}
            className={displayTab === 'balance' ? 'is-active' : ''}
            onClick={() => {
              setTab('balance');
              void fetchBalance();
            }}
          >
            Balance de Comprobación
          </button>
        )}
        {canViewConfig && (
          <button
            type="button"
            role="tab"
            aria-selected={displayTab === 'config'}
            className={displayTab === 'config' ? 'is-active' : ''}
            onClick={() => setTab('config')}
          >
            {t('accounting.tabs.config')}
          </button>
        )}
      </div>

      {displayTab === 'accounts' && canViewAccounts && <AccountingAccountsTab {...ctx} />}
      {displayTab === 'config' && canViewConfig && <AccountingConfigTab {...ctx} />}
      {displayTab === 'journal' && canViewJournal && <AccountingJournalTab {...ctx} />}
      {displayTab === 'mayor' && canViewJournal && <AccountingMayorTab {...ctx} />}
      {displayTab === 'balance' && canViewJournal && <AccountingBalanceTab {...ctx} />}
    </ErpPageTemplate>
  );
}
