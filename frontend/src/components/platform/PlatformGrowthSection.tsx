import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  ResponsiveContainer,
  ComposedChart,
  CartesianGrid,
  XAxis,
  YAxis,
  Tooltip,
  Legend,
  Bar,
  Line,
} from 'recharts';
import { EmptyState, LoadingState } from '../PageShell';
import { Card } from '../ui';
import { useI18n } from '../../i18n/i18n';
import { ZHBtn, ZHField } from '../zh/ZHForm';
import { ZHCardSection, ZHGridRow, ZHInlineRowRight } from '../zh/ZHLayout';
import { ZHPageNotice } from '../zh/ZHPageNotice';
import {
  platformService,
  type GrowthAnalyticsResponse,
  type GrowthMonetaryResponse,
} from '../../modules/platform/api/platformService';
import { formatApiRequestError } from '../../modules/lib/apiError';
import './PlatformGrowthSection.css';

function toYmd(d: Date): string {
  return d.toISOString().slice(0, 10);
}

function computeLast3Months(): { from: string; to: string } {
  const to = new Date();
  const from = new Date(to);
  from.setMonth(from.getMonth() - 3);
  return { from: toYmd(from), to: toYmd(to) };
}

function formatGrowthMoney(value: number, currencyHint: string): string {
  const n = Number.isFinite(value) ? value : 0;
  if (currencyHint === 'MIXED')
    return new Intl.NumberFormat(undefined, { maximumFractionDigits: 0 }).format(n);
  try {
    return new Intl.NumberFormat(undefined, {
      style: 'currency',
      currency: currencyHint,
      maximumFractionDigits: 0,
    }).format(n);
  } catch {
    return new Intl.NumberFormat(undefined, { maximumFractionDigits: 0 }).format(n);
  }
}

type GrowthTab = 'volumes' | 'monetary';

/** Crecimiento (cantidades + MRR): mismos filtros y gráficos que antes en `/platform/growth`. */
export function PlatformGrowthSection() {
  const { t } = useI18n();
  const init = useMemo(() => computeLast3Months(), []);
  const [from, setFrom] = useState(init.from);
  const [to, setTo] = useState(init.to);
  const [granularity, setGranularity] = useState<'auto' | 'day' | 'week' | 'month'>('auto');
  const [tab, setTab] = useState<GrowthTab>('volumes');
  const [dataCounts, setDataCounts] = useState<GrowthAnalyticsResponse | null>(null);
  const [dataMoney, setDataMoney] = useState<GrowthMonetaryResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const g = granularity === 'auto' ? undefined : granularity;
      const [counts, money] = await Promise.all([
        platformService.getGrowthAnalytics(from, to, g),
        platformService.getGrowthMonetaryAnalytics(from, to, g),
      ]);
      setDataCounts(counts);
      setDataMoney(money);
    } catch (e) {
      setError(formatApiRequestError(e, { offline: t('common.apiUnreachable'), generic: t('common.errorGeneric') }));
      setDataCounts(null);
      setDataMoney(null);
    } finally {
      setLoading(false);
    }
  }, [from, to, granularity, t]);

  useEffect(() => {
    void load();
  }, [load]);

  const chartRows = useMemo(() => dataCounts?.series ?? [], [dataCounts]);
  const moneyRows = useMemo(() => dataMoney?.series ?? [], [dataMoney]);
  const currencyHint = dataMoney?.currencyHint ?? 'USD';

  const applyLast3Months = () => {
    const r = computeLast3Months();
    setFrom(r.from);
    setTo(r.to);
  };

  const moneyTickFormatter = (v: number) => formatGrowthMoney(v, currencyHint);

  return (
    <div className="sa-growthSection">
      {error ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} /> : null}
      <p className="subtle sa-growthIntro">{t('platform.growth.subtitle')}</p>
      <Card>
        <ZHCardSection title={`${t('platform.growth.from')} / ${t('platform.growth.to')}`}>
          <p className="subtle sag-hint">{t('platform.growth.defaultRangeHint')}</p>
          <ZHGridRow cols={2}>
            <ZHField label={t('platform.growth.from')}>
              <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} disabled={loading} />
            </ZHField>
            <ZHField label={t('platform.growth.to')}>
              <input type="date" value={to} onChange={(e) => setTo(e.target.value)} disabled={loading} />
            </ZHField>
          </ZHGridRow>
          <ZHGridRow cols={2}>
            <ZHField label={t('platform.growth.granularity')}>
              <select
                value={granularity}
                onChange={(e) => setGranularity(e.target.value as typeof granularity)}
                disabled={loading}
              >
                <option value="auto">{t('platform.growth.granularityAuto')}</option>
                <option value="day">{t('platform.growth.granularityDay')}</option>
                <option value="week">{t('platform.growth.granularityWeek')}</option>
                <option value="month">{t('platform.growth.granularityMonth')}</option>
              </select>
            </ZHField>
            <ZHField label=" ">
              <ZHInlineRowRight>
                <ZHBtn variant="secondary" type="button" onClick={applyLast3Months} disabled={loading}>
                  {t('platform.growth.last3Months')}
                </ZHBtn>
                <ZHBtn variant="primary" type="button" onClick={() => void load()} disabled={loading}>
                  {t('platform.growth.apply')}
                </ZHBtn>
              </ZHInlineRowRight>
            </ZHField>
          </ZHGridRow>
          {dataCounts ? (
            <p className="subtle sag-meta">
              {t('platform.growth.usedGranularity')}: <strong>{dataCounts.granularity}</strong>
              {tab === 'monetary' && dataMoney ? (
                <>
                  {' · '}
                  {t('platform.growth.currencyHint')}: <strong>{dataMoney.currencyHint}</strong>
                </>
              ) : null}
            </p>
          ) : null}
        </ZHCardSection>
      </Card>

      <div className="sag-tabs" role="tablist" aria-label={t('platform.growth.tabsAria')}>
        <button
          type="button"
          role="tab"
          aria-selected={tab === 'volumes'}
          className={`sag-tab${tab === 'volumes' ? ' sag-tab--active' : ''}`}
          onClick={() => setTab('volumes')}
          disabled={loading}
        >
          {t('platform.growth.tabVolumes')}
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={tab === 'monetary'}
          className={`sag-tab${tab === 'monetary' ? ' sag-tab--active' : ''}`}
          onClick={() => setTab('monetary')}
          disabled={loading}
        >
          {t('platform.growth.tabMonetary')}
        </button>
      </div>

      {tab === 'monetary' ? (
        <p className="subtle sag-disclaimer">{t('platform.growth.moneyDisclaimer')}</p>
      ) : null}

      {loading ? (
        <Card>
          <LoadingState />
        </Card>
      ) : chartRows.length === 0 ? (
        <Card>
          <EmptyState message={t('platform.growth.empty')} />
        </Card>
      ) : tab === 'volumes' ? (
        <>
          <Card>
            <ZHCardSection title={t('platform.growth.chartNewTitle')}>
              <div className="sag-chart">
                <ResponsiveContainer width="100%" height="100%">
                  <ComposedChart data={chartRows} margin={{ top: 8, right: 8, bottom: 48, left: 8 }}>
                    <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" />
                    <XAxis dataKey="periodLabel" tick={{ fontSize: 11 }} interval={0} angle={-22} textAnchor="end" height={56} />
                    <YAxis allowDecimals={false} width={40} />
                    <Tooltip />
                    <Legend />
                    <Bar dataKey="newSubscribers" name={t('platform.growth.legend.subscribers')} fill="#0ea5e9" radius={[4, 4, 0, 0]} />
                    <Bar dataKey="newIdentityUsers" name={t('platform.growth.legend.users')} fill="#6366f1" radius={[4, 4, 0, 0]} />
                    <Bar dataKey="newCompanyUserMemberships" name={t('platform.growth.legend.company_user_memberships')} fill="#14b8a6" radius={[4, 4, 0, 0]} />
                  </ComposedChart>
                </ResponsiveContainer>
              </div>
            </ZHCardSection>
          </Card>
          <Card>
            <ZHCardSection title={t('platform.growth.chartCumulativeTitle')}>
              <div className="sag-chart">
                <ResponsiveContainer width="100%" height="100%">
                  <ComposedChart data={chartRows} margin={{ top: 8, right: 8, bottom: 48, left: 8 }}>
                    <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" />
                    <XAxis dataKey="periodLabel" tick={{ fontSize: 11 }} interval={0} angle={-22} textAnchor="end" height={56} />
                    <YAxis allowDecimals={false} width={44} />
                    <Tooltip />
                    <Legend />
                    <Line
                      type="monotone"
                      dataKey="cumulativeSubscribers"
                      name={t('platform.growth.legend.subscribers')}
                      stroke="#0ea5e9"
                      strokeWidth={2}
                      dot={{ r: 3 }}
                    />
                    <Line
                      type="monotone"
                      dataKey="cumulativeIdentityUsers"
                      name={t('platform.growth.legend.users')}
                      stroke="#6366f1"
                      strokeWidth={2}
                      dot={{ r: 3 }}
                    />
                    <Line
                      type="monotone"
                      dataKey="cumulativeCompanyUserMemberships"
                      name={t('platform.growth.legend.company_user_memberships')}
                      stroke="#14b8a6"
                      strokeWidth={2}
                      dot={{ r: 3 }}
                    />
                  </ComposedChart>
                </ResponsiveContainer>
              </div>
            </ZHCardSection>
          </Card>
        </>
      ) : moneyRows.length === 0 ? (
        <Card>
          <EmptyState message={t('platform.growth.empty')} />
        </Card>
      ) : (
        <>
          <Card>
            <ZHCardSection title={t('platform.growth.chartMoneyNewTitle')}>
              <div className="sag-chart">
                <ResponsiveContainer width="100%" height="100%">
                  <ComposedChart data={moneyRows} margin={{ top: 8, right: 8, bottom: 48, left: 8 }}>
                    <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" />
                    <XAxis dataKey="periodLabel" tick={{ fontSize: 11 }} interval={0} angle={-22} textAnchor="end" height={56} />
                    <YAxis tickFormatter={moneyTickFormatter} width={56} />
                    <Tooltip
                      formatter={(value: number | string) =>
                        formatGrowthMoney(typeof value === 'number' ? value : Number(value), currencyHint)
                      }
                    />
                    <Legend />
                    <Bar
                      dataKey="newMrrApprox"
                      name={t('platform.growth.legend.newMrr')}
                      fill="#0ea5e9"
                      radius={[4, 4, 0, 0]}
                    />
                  </ComposedChart>
                </ResponsiveContainer>
              </div>
            </ZHCardSection>
          </Card>
          <Card>
            <ZHCardSection title={t('platform.growth.chartMoneyCumulativeTitle')}>
              <div className="sag-chart">
                <ResponsiveContainer width="100%" height="100%">
                  <ComposedChart data={moneyRows} margin={{ top: 8, right: 8, bottom: 48, left: 8 }}>
                    <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" />
                    <XAxis dataKey="periodLabel" tick={{ fontSize: 11 }} interval={0} angle={-22} textAnchor="end" height={56} />
                    <YAxis tickFormatter={moneyTickFormatter} width={56} />
                    <Tooltip
                      formatter={(value: number | string) =>
                        formatGrowthMoney(typeof value === 'number' ? value : Number(value), currencyHint)
                      }
                    />
                    <Legend />
                    <Line
                      type="monotone"
                      dataKey="cumulativeMrrApprox"
                      name={t('platform.growth.legend.cumulativeMrr')}
                      stroke="#6366f1"
                      strokeWidth={2}
                      dot={{ r: 3 }}
                    />
                  </ComposedChart>
                </ResponsiveContainer>
              </div>
            </ZHCardSection>
          </Card>
        </>
      )}
    </div>
  );
}
