import { useState } from 'react';
import { GYM } from '@/config/gym';
import {
  Alert,
  Box,
  Chip,
  CircularProgress,
  Divider,
  Paper,
  Stack,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from '@mui/material';
import {
  Bar,
  CartesianGrid,
  ComposedChart,
  Line,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import { useQuery } from '@tanstack/react-query';
import { reportService } from '@services/reportService';
import { ResponsiveTable } from '@components/common';
import type { RevenueMonth, TakingsPayment } from '@app-types/index';

const usd = (amount: number) =>
  `$${amount.toLocaleString('en-US', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}`;

/** Axis labels want to be short, so no cents and no padding. */
const usdShort = (amount: number) => `$${Math.round(amount).toLocaleString('en-US')}`;

const lbp = (amount: number) => `LL ${Math.round(amount).toLocaleString('en-US')}`;

const showDate = (iso: string) =>
  new Date(iso).toLocaleDateString('en-GB', { day: 'numeric', month: 'short' });


/**
 * Revenue month by month, and any month opened up.
 *
 * Two things about this screen are deliberate and easy to undo by accident.
 *
 * **Bars, not a line, for the money.** A month is a bucket, not a point on a continuum -
 * a line between January and February implies the money flowed smoothly between them,
 * which is not a thing that happened. The member count *is* continuous, so it gets the
 * line, and the two together answer a question neither answers alone: falling members
 * under flat revenue is an early warning the money bars hide for a month or two.
 *
 * **The money is cash in, counted in the month it was taken.** A three-month package
 * bought in January sits entirely in January. That makes the bars lumpy, and it is still
 * right: it is what the drawer did, and it is what the daily takings report and the
 * dashboard already say. A chart that smoothed it would be the only screen in the system
 * disagreeing about March.
 */
const RevenuePage = () => {
  const [months, setMonths] = useState(12);
  const [openMonth, setOpenMonth] = useState<{ year: number; month: number } | null>(null);

  const { data, isLoading, isError } = useQuery({
    queryKey: ['reports', 'revenue', months],
    queryFn: () => reportService.getRevenueTrend(months),
  });

  return (
    <Box sx={{ maxWidth: 1100 }}>
      <Typography variant="h4" gutterBottom sx={{ fontWeight: 700 }}>
        Revenue
      </Typography>
      <Typography variant="body1" color="text.secondary" sx={{ mb: 3 }}>
        What the gym took each month, counted on the day the money arrived. Click a month to
        see every payment in it.
      </Typography>

      <ToggleButtonGroup
        size="small"
        exclusive
        value={months}
        onChange={(_e, value) => value && setMonths(value)}
        sx={{ mb: 3 }}
      >
        <ToggleButton value={6}>6 months</ToggleButton>
        <ToggleButton value={12}>12 months</ToggleButton>
        <ToggleButton value={24}>2 years</ToggleButton>
      </ToggleButtonGroup>

      {isLoading && (
        <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}>
          <CircularProgress />
        </Box>
      )}

      {isError && (
        <Alert severity="error">
          Could not load the revenue. Check the gym system is running, then reload.
        </Alert>
      )}

      {data && (
        <Stack spacing={3}>
          <Paper sx={{ p: { xs: 2, sm: 3 } }}>
            <Stack
              direction={{ xs: 'column', sm: 'row' }}
              spacing={3}
              sx={{ mb: 2 }}
              divider={<Divider orientation="vertical" flexItem />}
            >
              <Figure label="Taken over the period" value={usd(data.totalUsd)} />
              <Figure label="Average month" value={usd(data.averageMonthUsd)} />
              {data.bestMonthLabel && (
                <Figure
                  label="Best month"
                  value={usd(data.bestMonthUsd)}
                  hint={data.bestMonthLabel}
                />
              )}
            </Stack>

            {/* Fixed height: a responsive chart with no height collapses to nothing. */}
            <Box sx={{ height: 340, mx: -1 }}>
              <ResponsiveContainer width="100%" height="100%">
                <ComposedChart
                  data={data.months}
                  margin={{ top: 8, right: 8, bottom: 4, left: 0 }}
                  // Recharts 3 reports which column was clicked as an index, and does not
                  // wire clicks on individual bars at all - a per-bar handler compiles,
                  // renders and silently never fires. Reading the index back against the
                  // data we passed in is the shape the library actually supports.
                  onClick={(state) => {
                    const index = Number(state?.activeIndex);
                    const month = Number.isInteger(index) ? data.months[index] : undefined;
                    if (month) setOpenMonth({ year: month.year, month: month.month });
                  }}
                >
                  <CartesianGrid strokeDasharray="3 3" vertical={false} opacity={0.4} />
                  {/* Recharts drops labels that will not fit rather than overprinting them. */}
                  <XAxis dataKey="label" tick={{ fontSize: 12 }} interval="preserveStartEnd" minTickGap={12} />
                  <YAxis
                    yAxisId="money"
                    tickFormatter={usdShort}
                    tick={{ fontSize: 12 }}
                    width={70}
                  />
                  <YAxis
                    yAxisId="members"
                    orientation="right"
                    tick={{ fontSize: 12 }}
                    width={40}
                    allowDecimals={false}
                  />
                  <Tooltip content={<MonthTooltip />} />

                  <Bar
                    yAxisId="money"
                    dataKey="totalUsd"
                    name="Taken"
                    fill={GYM.colour.main}
                    radius={[3, 3, 0, 0]}
                    cursor="pointer"
                  />
                  <Line
                    yAxisId="members"
                    type="monotone"
                    dataKey="activeMembers"
                    name="Members"
                    stroke="#9c6a0b"
                    strokeWidth={2}
                    dot={{ r: 3 }}
                  />
                </ComposedChart>
              </ResponsiveContainer>
            </Box>

            {/*
              The months again, as buttons.

              Not decoration. Recharts decides which column was clicked from the hover it
              had a moment earlier, and a tap has no hover before it - so on a phone the
              chart opens whatever month happened to be active last, or the first one.
              These always work, and on a narrow screen where twelve axis labels collapse
              into an unreadable smear they are the only way to tell the months apart.
            */}
            <Box
              sx={{
                display: 'flex',
                gap: 0.75,
                overflowX: 'auto',
                pb: 1,
                mt: 1,
                // The row scrolls rather than the page: a chart that pushed the whole
                // screen sideways would be worse than the crowding it is fixing.
                '&::-webkit-scrollbar': { height: 6 },
              }}
            >
              {data.months.map((month) => (
                <Chip
                  key={month.label}
                  label={`${month.label} · ${usdShort(month.totalUsd)}`}
                  size="small"
                  onClick={() =>
                    setOpenMonth({ year: month.year, month: month.month })
                  }
                  color={
                    openMonth?.year === month.year && openMonth?.month === month.month
                      ? 'primary'
                      : 'default'
                  }
                  variant={
                    openMonth?.year === month.year && openMonth?.month === month.month
                      ? 'filled'
                      : 'outlined'
                  }
                  sx={{ flexShrink: 0 }}
                />
              ))}
            </Box>

            <Typography variant="caption" color="text.secondary">
              Bars are money taken, on the left. The line is how many members could train,
              on the right. A payment counts in the month it was handed over, so a
              three-month package sits entirely in the month it was bought.
            </Typography>
          </Paper>

          {openMonth ? (
            <MonthDetail
              year={openMonth.year}
              month={openMonth.month}
              onClose={() => setOpenMonth(null)}
            />
          ) : (
            <Alert severity="info">Click a month on the chart to see what made it up.</Alert>
          )}
        </Stack>
      )}
    </Box>
  );
};

/**
 * The hover card.
 *
 * Recharts' default tooltip would print the raw series values with no currency and no
 * sense of whether the month is finished. This one says what the month actually was.
 */
const MonthTooltip = ({
  active,
  payload,
}: {
  active?: boolean;
  payload?: { payload: RevenueMonth }[];
}) => {
  if (!active || !payload?.length) return null;

  const month = payload[0].payload;

  return (
    <Paper sx={{ p: 1.5, minWidth: 190 }} elevation={4}>
      <Typography variant="subtitle2" sx={{ mb: 0.5 }}>
        {month.label}
        {month.inProgress && (
          <Chip label="so far" size="small" sx={{ ml: 1 }} variant="outlined" />
        )}
      </Typography>

      <TooltipRow label="Taken" value={usd(month.totalUsd)} />
      <TooltipRow label="Drawer" value={usd(month.drawerUsd)} />
      <TooltipRow label="Whish" value={usd(month.whishUsd)} />
      <TooltipRow label="Members" value={`${month.activeMembers}`} />
      {month.reversalCount > 0 && (
        <TooltipRow label="Refunded" value={usd(Math.abs(month.reversalsUsd))} />
      )}
    </Paper>
  );
};

const TooltipRow = ({ label, value }: { label: string; value: string }) => (
  <Stack direction="row" justifyContent="space-between" spacing={2}>
    <Typography variant="caption" color="text.secondary">
      {label}
    </Typography>
    <Typography variant="caption">{value}</Typography>
  </Stack>
);

/** One month opened up, in the same shape the daily takings report uses for a day. */
const MonthDetail = ({
  year,
  month,
  onClose,
}: {
  year: number;
  month: number;
  onClose: () => void;
}) => {
  const { data, isLoading } = useQuery({
    queryKey: ['reports', 'revenue', year, month],
    queryFn: () => reportService.getRevenueMonth(year, month),
  });

  if (isLoading) {
    return (
      <Paper sx={{ p: 3, display: 'flex', justifyContent: 'center' }}>
        <CircularProgress size={24} />
      </Paper>
    );
  }

  if (!data) return null;

  return (
    <Paper sx={{ p: { xs: 2, sm: 3 } }}>
      <Stack
        direction="row"
        justifyContent="space-between"
        alignItems="baseline"
        flexWrap="wrap"
        sx={{ mb: 2 }}
      >
        <Typography variant="h6">{data.label}</Typography>
        <Typography
          variant="body2"
          sx={{ cursor: 'pointer', color: 'primary.main' }}
          onClick={onClose}
        >
          Close
        </Typography>
      </Stack>

      <Stack
        direction={{ xs: 'column', sm: 'row' }}
        spacing={3}
        sx={{ mb: 3 }}
        divider={<Divider orientation="vertical" flexItem />}
      >
        <Figure label="Taken" value={usd(data.totalUsd)} />
        <Figure
          label="Into the drawer"
          value={usd(data.drawerUsd)}
          hint={data.cashLbpReceived !== 0 ? `incl. ${lbp(data.cashLbpReceived)}` : undefined}
        />
        <Figure label="Whish Money" value={usd(data.whishUsd)} />
        <Figure
          label="Memberships renewed"
          value={`${data.renewalCount}`}
          hint={`${data.paymentCount} payments`}
        />
      </Stack>

      {data.reversalCount > 0 && (
        <Alert severity="info" sx={{ mb: 2 }}>
          {usd(Math.abs(data.reversalsUsd))} was handed back in {data.label}, and is already
          taken off the figures above.
        </Alert>
      )}

      <Divider sx={{ mb: 2 }} />

      <ResponsiveTable<TakingsPayment>
        rows={data.payments}
        rowKey={(row) => row.id}
        emptyMessage="Nothing was taken in this month"
        columns={[
          {
            header: 'USD',
            primary: true,
            render: (r) => (
              <Typography
                component="span"
                sx={{ fontWeight: 600, color: r.amountUsd < 0 ? 'warning.main' : 'inherit' }}
              >
                {usd(r.amountUsd)}
              </Typography>
            ),
          },
          {
            header: 'Kind',
            badge: true,
            render: (r) =>
              r.isReversal ? (
                <Chip label="Refund" color="warning" size="small" variant="outlined" />
              ) : null,
          },
          { header: 'Member', render: (r) => r.clientName },
          { header: 'Day', hideOnPhone: true, render: (r) => showDate(r.takenAt) },
          { header: 'Package', hideOnPhone: true, render: (r) => r.packageName },
          { header: 'Method', render: (r) => r.paymentMethod },
          {
            header: 'Handed over',
            align: 'right',
            hideOnPhone: true,
            render: (r) =>
              r.currency === 'Lbp' ? lbp(r.amountReceived) : usd(r.amountReceived),
          },
        ]}
      />
    </Paper>
  );
};

const Figure = ({
  label,
  value,
  hint,
}: {
  label: string;
  value: string;
  hint?: string;
}) => (
  <Box>
    <Typography variant="overline" color="text.secondary" display="block">
      {label}
    </Typography>
    <Typography variant="h5" sx={{ fontWeight: 700 }}>
      {value}
    </Typography>
    {hint && (
      <Typography variant="caption" color="text.secondary">
        {hint}
      </Typography>
    )}
  </Box>
);

export default RevenuePage;
