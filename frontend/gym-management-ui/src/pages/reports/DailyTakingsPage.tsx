import { useState } from 'react';
import {
  Alert,
  Box,
  Chip,
  CircularProgress,
  Divider,
  IconButton,
  Paper,
  Stack,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material';
import { ChevronLeft, ChevronRight, Today } from '@mui/icons-material';
import { useQuery } from '@tanstack/react-query';
import { ResponsiveTable } from '@components/common';
import { reportService } from '@services/reportService';
import { DailyTakings, TakingsPayment } from '@app-types/index';

const usd = (amount: number) =>
  `$${amount.toLocaleString('en-US', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}`;

const lbp = (amount: number) => `LL ${Math.round(amount).toLocaleString('en-US')}`;

/** yyyy-MM-dd from the browser's own calendar, used only as a starting point. */
const todayIso = () => {
  const now = new Date();
  const month = `${now.getMonth() + 1}`.padStart(2, '0');
  const day = `${now.getDate()}`.padStart(2, '0');
  return `${now.getFullYear()}-${month}-${day}`;
};

const shiftDay = (iso: string, days: number) => {
  const [year, month, day] = iso.split('-').map(Number);
  const shifted = new Date(Date.UTC(year, month - 1, day + days));
  return shifted.toISOString().slice(0, 10);
};

const showTime = (iso: string) =>
  new Date(iso).toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' });

/**
 * The daily takings report.
 *
 * Arranged around one question: how much cash should be in the drawer right now. That
 * figure sits on its own, and everything that did not touch the till — Whish transfers —
 * is deliberately kept out of it and shown separately. A report that mixes the two stops
 * matching the count within a week, and a report that stops matching stops being used.
 */
const DailyTakingsPage = () => {
  const [date, setDate] = useState(todayIso());

  const { data, isLoading, isError } = useQuery({
    queryKey: ['reports', 'daily-takings', date],
    queryFn: () => reportService.getDailyTakings(date),
  });

  return (
    <Box sx={{ maxWidth: 1000 }}>
      <Typography variant="h4" gutterBottom sx={{ fontWeight: 700 }}>
        Daily takings
      </Typography>
      <Typography variant="body1" color="text.secondary" sx={{ mb: 3 }}>
        What came in on one day, split so you can count the drawer against it.
      </Typography>

      <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 3 }}>
        <IconButton onClick={() => setDate(shiftDay(date, -1))} aria-label="Previous day">
          <ChevronLeft />
        </IconButton>
        <TextField
          type="date"
          size="small"
          value={date}
          onChange={(e) => e.target.value && setDate(e.target.value)}
          sx={{ width: 190 }}
        />
        <IconButton
          onClick={() => setDate(shiftDay(date, 1))}
          disabled={date >= todayIso()}
          aria-label="Next day"
        >
          <ChevronRight />
        </IconButton>
        <Tooltip title="Back to today">
          <span>
            <IconButton onClick={() => setDate(todayIso())} disabled={date === todayIso()}>
              <Today />
            </IconButton>
          </span>
        </Tooltip>
      </Stack>

      {isLoading && (
        <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}>
          <CircularProgress />
        </Box>
      )}

      {isError && (
        <Alert severity="error">
          Could not load the takings. Check that the API is running, then reload this page.
        </Alert>
      )}

      {data && <TakingsBody takings={data} />}
    </Box>
  );
};

const TakingsBody = ({ takings }: { takings: DailyTakings }) => (
  <>
    <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} sx={{ mb: 3 }}>
      <Paper sx={{ p: 3, flex: 1, borderTop: 3, borderColor: 'success.main' }}>
        <Typography variant="overline" color="text.secondary">
          Should be in the drawer
        </Typography>
        <Typography variant="h3" sx={{ fontWeight: 700, color: 'success.main', mb: 1.5 }}>
          {usd(takings.drawerTotalUsd)}
        </Typography>

        <Line label="USD cash" value={usd(takings.cashUsd)} />
        {takings.cashLbpReceived !== 0 && (
          <Line
            label="LBP cash"
            value={usd(takings.cashLbpInUsd)}
            // The notes are what actually gets counted; the USD figure is what totals up.
            hint={lbp(takings.cashLbpReceived)}
          />
        )}
      </Paper>

      <Paper sx={{ p: 3, flex: 1 }}>
        <Typography variant="overline" color="text.secondary">
          Came in, not in the drawer
        </Typography>
        <Typography variant="h3" sx={{ fontWeight: 700, mb: 1.5 }}>
          {usd(takings.whishUsd + takings.otherUsd)}
        </Typography>

        <Line label="Whish Money" value={usd(takings.whishUsd)} />
        {takings.otherUsd !== 0 && <Line label="Other" value={usd(takings.otherUsd)} />}
      </Paper>
    </Stack>

    <Paper sx={{ p: 3, mb: 3 }}>
      <Stack direction="row" justifyContent="space-between" alignItems="baseline">
        <Typography variant="h6">Total for the day</Typography>
        <Typography variant="h4" sx={{ fontWeight: 700 }}>
          {usd(takings.totalUsd)}
        </Typography>
      </Stack>
      <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
        {takings.paymentCount} payment{takings.paymentCount === 1 ? '' : 's'}
        {takings.reversalCount > 0 &&
          `, ${takings.reversalCount} refund${takings.reversalCount === 1 ? '' : 's'}`}
      </Typography>

      {takings.reversalCount > 0 && (
        <Alert severity="info" sx={{ mt: 2 }}>
          {usd(Math.abs(takings.reversalsUsd))} was handed back today. It is already taken
          off the figures above — the drawer being lighter by that much is expected, not a
          shortfall.
        </Alert>
      )}
    </Paper>

    {takings.payments.length > 0 && (
      <>
        <Divider sx={{ mb: 2 }} />
        <Typography variant="h6" sx={{ mb: 1.5 }}>
          Every movement
        </Typography>
        <ResponsiveTable<TakingsPayment>
          rows={takings.payments}
          rowKey={(row) => row.id}
          emptyMessage="Nothing taken on this day"
          columns={[
            {
              header: 'USD',
              primary: true,
              render: (r) => (
                <Typography
                  component="span"
                  sx={{
                    fontWeight: 600,
                    color: r.amountUsd < 0 ? 'warning.main' : 'inherit',
                  }}
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
                  <Chip
                    label="Refund"
                    size="small"
                    color="warning"
                    variant="outlined"
                    sx={{ height: 20 }}
                  />
                ) : null,
            },
            { header: 'Member', render: (r) => r.clientName },
            { header: 'Time', render: (r) => showTime(r.takenAt) },
            { header: 'Package', render: (r) => r.packageName },
            {
              header: 'Method',
              render: (r) => (r.paymentMethod === 'Whish' ? 'Whish Money' : r.paymentMethod),
            },
            {
              header: 'Handed over',
              align: 'right',
              render: (r) =>
                r.currency === 'Lbp' ? (
                  <Tooltip
                    title={`Converted at ${Math.round(
                      r.exchangeRate ?? 0
                    ).toLocaleString()} LBP per $`}
                  >
                    <span>{lbp(r.amountReceived)}</span>
                  </Tooltip>
                ) : (
                  usd(r.amountReceived)
                ),
            },
          ]}
        />
      </>
    )}
  </>
);

const Line = ({
  label,
  value,
  hint,
}: {
  label: string;
  value: string;
  hint?: string;
}) => (
  <Stack direction="row" justifyContent="space-between" alignItems="baseline" sx={{ py: 0.4 }}>
    <Typography variant="body2" color="text.secondary">
      {label}
      {hint && (
        <Box component="span" sx={{ ml: 1, fontVariantNumeric: 'tabular-nums' }}>
          ({hint})
        </Box>
      )}
    </Typography>
    <Typography variant="body1" sx={{ fontWeight: 600 }}>
      {value}
    </Typography>
  </Stack>
);
export default DailyTakingsPage;
