import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Divider,
  IconButton,
  Link,
  Paper,
  Stack,
  Tooltip,
  Typography,
} from '@mui/material';
import { Link as RouterLink } from 'react-router-dom';
import CallIcon from '@mui/icons-material/Call';
import WhatsAppIcon from '@mui/icons-material/WhatsApp';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import RadioButtonUncheckedIcon from '@mui/icons-material/RadioButtonUnchecked';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { dashboardService } from '@services/dashboardService';
import { contactLinks } from '@lib/contact';
import { useAuthStore } from '@store/authStore';
import type { NeedsChasing, Today } from '@app-types/index';

const usd = (amount: number) =>
  `$${amount.toLocaleString('en-US', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}`;

const lbp = (amount: number) => `LL ${Math.round(amount).toLocaleString('en-US')}`;

/**
 * How a member's standing reads on the call sheet.
 *
 * Spelled out in days rather than left as a status word, because the number is the reason
 * to ring: "gone 12 days" and "3 days left" are two completely different conversations,
 * and "Expired" tells you neither.
 */
const describeStanding = (member: NeedsChasing): string => {
  const days = member.daysRemaining;

  if (days === null || days === undefined) return 'No end date';
  if (days < 0) {
    const ago = Math.abs(days);
    return `Gone ${ago} ${ago === 1 ? 'day' : 'days'}`;
  }
  if (days === 0) return 'Last day';
  return `${days} ${days === 1 ? 'day' : 'days'} left`;
};

/**
 * The first screen of the day.
 *
 * This replaced four stat cards, one of which was all-time revenue - a number that only
 * ever goes up and that nobody can do anything about. Everything here is something the
 * person reading it acts on this morning: count the drawer, ring these people, chase this
 * money. That is the whole design brief.
 *
 * Reception sees it too. Every figure on it is already theirs; the one they are not shown -
 * revenue history - is deliberately not on this screen at all.
 */
const TodayPage = () => {
  const { data, isLoading, isError } = useQuery({
    queryKey: ['dashboard', 'today'],
    queryFn: () => dashboardService.getToday(),
  });

  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', mt: 6 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (isError || !data) {
    return (
      <Alert severity="error">
        Could not load today. Check the gym system is running, then reload this page.
      </Alert>
    );
  }

  const today = new Date(data.date).toLocaleDateString('en-GB', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
  });

  return (
    <Box sx={{ maxWidth: 1100 }}>
      <Typography variant="h4" sx={{ fontWeight: 700 }}>
        Today
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
        {today}
      </Typography>

      <Stack spacing={3}>
        <Drawer data={data} />
        <ThisMonth />
        <CallSheet data={data} />
        <Owing data={data} />
      </Stack>
    </Box>
  );
};

/** What should be in the till right now, and what came in without touching it. */
const Drawer = ({ data }: { data: Today }) => (
  <Stack direction={{ xs: 'column', md: 'row' }} spacing={2}>
    <Paper sx={{ p: 3, flex: 1, borderTop: 3, borderColor: 'success.main' }}>
      <Typography variant="overline" color="text.secondary">
        Should be in the drawer
      </Typography>
      <Typography variant="h3" sx={{ fontWeight: 700, color: 'success.main', mb: 1.5 }}>
        {usd(data.drawerTotalUsd)}
      </Typography>

      <Stack spacing={0.5}>
        <Row label="USD cash" value={usd(data.cashUsd)} />
        {data.cashLbpReceived !== 0 && (
          <Row
            label="LBP cash"
            value={usd(data.cashLbpInUsd)}
            // The notes are what actually gets counted; the USD figure is what totals up.
            hint={lbp(data.cashLbpReceived)}
          />
        )}
      </Stack>

      {data.reversalCount > 0 && (
        <Alert severity="info" sx={{ mt: 2 }}>
          {usd(Math.abs(data.reversalsUsd))} was handed back today and is already taken off
          the figure above — the drawer being lighter by that much is expected.
        </Alert>
      )}
    </Paper>

    <Paper sx={{ p: 3, flex: 1 }}>
      <Typography variant="overline" color="text.secondary">
        Came in, not in the drawer
      </Typography>
      <Typography variant="h3" sx={{ fontWeight: 700, mb: 1.5 }}>
        {usd(data.whishUsd + data.otherUsd)}
      </Typography>

      <Stack spacing={0.5}>
        <Row label="Whish Money" value={usd(data.whishUsd)} />
        {data.otherUsd !== 0 && <Row label="Other" value={usd(data.otherUsd)} />}
      </Stack>

      <Divider sx={{ my: 2 }} />

      <Row label="Taken today, all in" value={usd(data.totalUsd)} />
      <Row
        label="Memberships renewed"
        value={`${data.renewalsToday}`}
        hint={data.paymentCount === 0 ? undefined : `${data.paymentCount} payments`}
      />
    </Paper>
  </Stack>
);

/**
 * How the month is going. Only the owner sees it.
 *
 * Month-to-date is revenue history, which is the one thing reception is deliberately not
 * shown, so this fetches separately from an admin-only endpoint rather than riding along
 * on the shared Today call.
 *
 * The headline is the month so far, compared against the same day of last month. Comparing
 * against last month's *total* would mean that on the 3rd every month looks 90% down, and
 * a figure that is alarming by construction gets ignored within a week.
 */
const ThisMonth = () => {
  const isAdmin = useAuthStore((state) => state.isAdmin)();

  const { data } = useQuery({
    queryKey: ['dashboard', 'this-month'],
    queryFn: () => dashboardService.getMonthSoFar(),
    enabled: isAdmin,
  });

  if (!isAdmin || !data) return null;

  const difference = data.thisMonthUsd - data.samePointLastMonthUsd;
  const ahead = difference >= 0;

  return (
    <Paper sx={{ p: 3 }}>
      <Stack
        direction="row"
        justifyContent="space-between"
        alignItems="baseline"
        flexWrap="wrap"
        sx={{ mb: 2 }}
      >
        <Typography variant="h6">This month</Typography>
        <Typography variant="caption" color="text.secondary">
          Day {data.dayOfMonth}
        </Typography>
      </Stack>

      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={3} alignItems="baseline">
        <Box>
          <Typography variant="h3" sx={{ fontWeight: 700 }}>
            {usd(data.thisMonthUsd)}
          </Typography>
          <Chip
            size="small"
            color={ahead ? 'success' : 'warning'}
            variant="outlined"
            label={`${ahead ? 'Ahead by' : 'Behind by'} ${usd(Math.abs(difference))}`}
          />
        </Box>

        <Divider orientation="vertical" flexItem sx={{ display: { xs: 'none', sm: 'block' } }} />

        <Stack spacing={0.5} sx={{ minWidth: 220 }}>
          <Row
            label="Same point last month"
            value={usd(data.samePointLastMonthUsd)}
          />
          <Row label="All of last month" value={usd(data.lastMonthTotalUsd)} />
          <Row label="Members who can train" value={`${data.activeMembers}`} />
          {/* Context, not a target - it only ever goes up. Kept small for that reason. */}
          <Row label="Taken all time" value={usd(data.allTimeUsd)} />
        </Stack>
      </Stack>
    </Paper>
  );
};

/**
 * The people worth ringing this morning, with the phone right there.
 *
 * Lapsed members come first. The old expiring list only ever showed people who had not
 * left yet; the ones already gone are exactly who a call wins back.
 */
const CallSheet = ({ data }: { data: Today }) => {
  const queryClient = useQueryClient();

  const mark = useMutation({
    mutationFn: ({ clientId, called }: { clientId: number; called: boolean }) =>
      dashboardService.markChased(clientId, called),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['dashboard', 'today'] });
    },
  });

  return (
    <Paper sx={{ p: 3 }}>
      <Stack
        direction="row"
        justifyContent="space-between"
        alignItems="baseline"
        flexWrap="wrap"
        sx={{ mb: 0.5 }}
      >
        <Typography variant="h6">Who needs chasing</Typography>
        {data.needsChasing.length > 0 && (
          <Typography variant="body2" color="text.secondary">
            {data.calledToday} of {data.needsChasing.length} called today
          </Typography>
        )}
      </Stack>

      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        Members about to run out, and the ones who already have. Frozen memberships are left
        out — they asked to pause.
      </Typography>

      {data.needsChasing.length === 0 ? (
        <Alert severity="success">
          Nobody to chase. Every membership is either comfortable or paused.
        </Alert>
      ) : (
        <Stack divider={<Divider />}>
          {data.needsChasing.map((member) => {
            const links = contactLinks(member.phoneDigits);
            const lapsed = (member.daysRemaining ?? 0) < 0;

            return (
              <Stack
                key={member.clientId}
                direction="row"
                alignItems="center"
                spacing={1}
                sx={{
                  py: 1.25,
                  // Called rows recede rather than disappear, so the list keeps its shape
                  // as it is worked through and nobody loses their place.
                  opacity: member.calledToday ? 0.55 : 1,
                }}
              >
                <Tooltip
                  title={member.calledToday ? 'Called today — undo' : 'Mark as called'}
                >
                  <IconButton
                    size="small"
                    color={member.calledToday ? 'success' : 'default'}
                    onClick={() =>
                      mark.mutate({
                        clientId: member.clientId,
                        called: !member.calledToday,
                      })
                    }
                    aria-label={
                      member.calledToday
                        ? `Undo called for ${member.clientName}`
                        : `Mark ${member.clientName} as called`
                    }
                  >
                    {member.calledToday ? (
                      <CheckCircleIcon fontSize="small" />
                    ) : (
                      <RadioButtonUncheckedIcon fontSize="small" />
                    )}
                  </IconButton>
                </Tooltip>

                <Box sx={{ flexGrow: 1, minWidth: 0 }}>
                  <Link
                    component={RouterLink}
                    to={`/admin/clients/${member.clientId}`}
                    underline="hover"
                    color="inherit"
                    sx={{ fontWeight: 500 }}
                  >
                    {member.clientName}
                  </Link>
                  <Typography variant="caption" color="text.secondary" display="block">
                    {member.phoneNumber}
                    {member.packageName ? ` · ${member.packageName}` : ''}
                  </Typography>
                </Box>

                <Chip
                  size="small"
                  label={describeStanding(member)}
                  color={lapsed ? 'error' : 'warning'}
                  variant="outlined"
                />

                {links && (
                  <>
                    <Tooltip title="Call">
                      <IconButton
                        size="small"
                        component="a"
                        href={links.tel}
                        aria-label={`Call ${member.clientName}`}
                      >
                        <CallIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title="WhatsApp">
                      <IconButton
                        size="small"
                        component="a"
                        href={links.whatsapp}
                        target="_blank"
                        rel="noopener"
                        aria-label={`WhatsApp ${member.clientName}`}
                      >
                        <WhatsAppIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  </>
                )}
              </Stack>
            );
          })}
        </Stack>
      )}
    </Paper>
  );
};

/** Money already handed over that did not cover a package, and who still owes it. */
const Owing = ({ data }: { data: Today }) => (
  <Paper sx={{ p: 3 }}>
    <Stack
      direction="row"
      justifyContent="space-between"
      alignItems="baseline"
      flexWrap="wrap"
      sx={{ mb: 2 }}
    >
      <Typography variant="h6">Who owes money</Typography>
      <Typography variant="h6" color={data.totalOwed > 0 ? 'error.main' : 'text.secondary'}>
        {usd(data.totalOwed)}
      </Typography>
    </Stack>

    {data.owes.length === 0 ? (
      <Alert severity="success">Nobody is part-paid. Everything is settled.</Alert>
    ) : (
      <>
        <Stack divider={<Divider />}>
          {data.owes.map((member) => (
            <Stack
              key={member.clientId}
              direction="row"
              justifyContent="space-between"
              alignItems="center"
              sx={{ py: 1.25 }}
            >
              <Box>
                <Link
                  component={RouterLink}
                  to={`/admin/clients/${member.clientId}`}
                  underline="hover"
                  color="inherit"
                  sx={{ fontWeight: 500 }}
                >
                  {member.clientName}
                </Link>
                <Typography variant="caption" color="text.secondary" display="block">
                  {member.daysOutstanding} {member.daysOutstanding === 1 ? 'day' : 'days'}{' '}
                  outstanding
                </Typography>
              </Box>
              <Typography sx={{ fontWeight: 600 }} color="error.main">
                {usd(member.amountOwed)}
              </Typography>
            </Stack>
          ))}
        </Stack>

        {data.owesCount > data.owes.length && (
          <Button
            component={RouterLink}
            to="/admin/reports/who-owes"
            size="small"
            sx={{ mt: 1.5 }}
          >
            See all {data.owesCount}
          </Button>
        )}
      </>
    )}
  </Paper>
);

const Row = ({
  label,
  value,
  hint,
}: {
  label: string;
  value: string;
  hint?: string;
}) => (
  <Stack direction="row" justifyContent="space-between" alignItems="baseline">
    <Typography variant="body2" color="text.secondary">
      {label}
    </Typography>
    <Box sx={{ textAlign: 'right' }}>
      <Typography variant="body2" component="span">
        {value}
      </Typography>
      {hint && (
        <Typography variant="caption" color="text.secondary" display="block">
          {hint}
        </Typography>
      )}
    </Box>
  </Stack>
);

export default TodayPage;
