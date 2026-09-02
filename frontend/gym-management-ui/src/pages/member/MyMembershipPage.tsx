import {
  Alert,
  AppBar,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  Container,
  Divider,
  Stack,
  Toolbar,
  Typography,
} from '@mui/material';
import { useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { memberService } from '@services/memberService';
import { useAuthStore } from '@store/authStore';
import { authService } from '@services/authService';
import type { MembershipStatusString, MyMembership, Payment } from '@app-types/index';

/** How each status should read and colour on the member's own page. */
const STATUS_PRESENTATION: Record<
  MembershipStatusString,
  { label: string; colour: 'success' | 'warning' | 'error' | 'info' | 'default' }
> = {
  Active: { label: 'Active', colour: 'success' },
  Expiring: { label: 'Ending soon', colour: 'warning' },
  Expired: { label: 'Expired', colour: 'error' },
  Suspended: { label: 'Frozen', colour: 'info' },
  Pending: { label: 'Not started', colour: 'default' },
};

const formatDate = (value?: string) =>
  value ? new Date(value).toLocaleDateString('en-GB', {
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  }) : '-';

const formatMoney = (amount: number) =>
  `${amount < 0 ? '-' : ''}$${Math.abs(amount).toFixed(2)}`;

/**
 * The headline sentence, which is the whole reason an expired member is allowed to sign in.
 *
 * "Expired" on its own is a dead end. "Ended 12 days ago" with a renew prompt is the thing
 * that brings somebody back, so the number is spelled out rather than left as a status word.
 */
const describeStanding = (membership: MyMembership): string => {
  const days = membership.daysRemaining;

  if (membership.isSuspended) {
    return 'Your membership is frozen. Speak to the gym when you are ready to start again.';
  }

  if (days === undefined || days === null) {
    return 'Your membership has not started yet. Come to the desk to pay and get going.';
  }

  if (days < 0) {
    const ago = Math.abs(days);
    return `Your membership ended ${ago} ${ago === 1 ? 'day' : 'days'} ago. Come to the desk to renew.`;
  }

  if (days === 0) {
    return 'Today is the last day of your membership. Renew at the desk to keep training.';
  }

  return `${days} ${days === 1 ? 'day' : 'days'} left on your membership.`;
};

/**
 * A member's own area: what they have, how long is left, and what they have paid.
 *
 * Everything here comes from `/me`, which resolves the membership from the signed-in user.
 * There is no member id anywhere on this page, and so no id for one member to change into
 * somebody else's.
 */
const MyMembershipPage = () => {
  const navigate = useNavigate();
  const { user, refreshToken, logout } = useAuthStore();

  const membershipQuery = useQuery({
    queryKey: ['my-membership'],
    queryFn: () => memberService.getMyMembership(),
    retry: false,
  });

  const paymentsQuery = useQuery({
    queryKey: ['my-payments'],
    queryFn: () => memberService.getMyPayments(),
    retry: false,
  });

  const handleSignOut = async () => {
    try {
      if (refreshToken) await authService.logout(refreshToken);
    } finally {
      logout();
      navigate('/login', { replace: true });
    }
  };

  const membership = membershipQuery.data;

  return (
    <Box sx={{ minHeight: '100vh', bgcolor: 'background.default' }}>
      <AppBar position="static">
        <Toolbar>
          <Typography variant="h6" sx={{ flexGrow: 1 }}>
            My membership
          </Typography>
          <Typography variant="body2" sx={{ mr: 2, display: { xs: 'none', sm: 'block' } }}>
            {user?.fullName}
          </Typography>
          <Button color="inherit" onClick={handleSignOut}>
            Sign out
          </Button>
        </Toolbar>
      </AppBar>

      <Container maxWidth="sm" sx={{ py: 3 }}>
        {membershipQuery.isLoading && (
          <Box sx={{ display: 'flex', justifyContent: 'center', py: 6 }}>
            <CircularProgress />
          </Box>
        )}

        {/*
          The account signs in fine but has no membership behind it any more, which happens
          once the gym removes the member record. Saying so plainly beats an empty page.
        */}
        {membershipQuery.isError && (
          <Alert severity="warning">
            We could not find a membership attached to this account. Please speak to the gym.
          </Alert>
        )}

        {membership && (
          <Stack spacing={2}>
            <Card>
              <CardContent>
                <Stack
                  direction="row"
                  spacing={1}
                  alignItems="center"
                  justifyContent="space-between"
                  sx={{ mb: 1 }}
                >
                  <Typography variant="h5">{membership.fullName}</Typography>
                  <Chip
                    label={STATUS_PRESENTATION[membership.membershipStatus].label}
                    color={STATUS_PRESENTATION[membership.membershipStatus].colour}
                    size="small"
                  />
                </Stack>

                <Typography variant="body1" sx={{ mb: 2 }}>
                  {describeStanding(membership)}
                </Typography>

                {/*
                  Expiring is not Expired. A member in their last paid week can still train,
                  and telling them otherwise would turn away somebody who has paid.
                */}
                {membership.canTrainToday ? (
                  <Alert severity="success" sx={{ mb: 1 }}>
                    You can train today.
                  </Alert>
                ) : (
                  <Alert severity="warning" sx={{ mb: 1 }}>
                    You cannot train today until this is renewed.
                  </Alert>
                )}

                <Divider sx={{ my: 2 }} />

                <Stack spacing={1}>
                  <Row label="Package" value={membership.currentPackageName ?? '-'} />
                  <Row label="Started" value={formatDate(membership.membershipStartDate)} />
                  <Row label="Ends" value={formatDate(membership.membershipEndDate)} />
                  <Row label="Phone" value={membership.phoneNumber} />
                </Stack>

                {membership.outstandingCredit > 0 && (
                  <Alert severity="info" sx={{ mt: 2 }}>
                    You have {formatMoney(membership.outstandingCredit)} paid towards your
                    next package. It comes off what you owe when you next pay.
                  </Alert>
                )}
              </CardContent>
            </Card>

            <Card>
              <CardContent>
                <Typography variant="h6" sx={{ mb: 1 }}>
                  Payments
                </Typography>

                {paymentsQuery.isLoading && <CircularProgress size={20} />}

                {paymentsQuery.data?.length === 0 && (
                  <Typography variant="body2" color="text.secondary">
                    Nothing recorded yet.
                  </Typography>
                )}

                <Stack divider={<Divider />} spacing={0}>
                  {paymentsQuery.data?.map((payment) => (
                    <PaymentRow key={payment.id} payment={payment} />
                  ))}
                </Stack>
              </CardContent>
            </Card>
          </Stack>
        )}
      </Container>
    </Box>
  );
};

const Row = ({ label, value }: { label: string; value: string }) => (
  <Stack direction="row" justifyContent="space-between">
    <Typography variant="body2" color="text.secondary">
      {label}
    </Typography>
    <Typography variant="body2">{value}</Typography>
  </Stack>
);

/**
 * One payment. A reversal is shown as a correction rather than hidden: the member sees the
 * original and the money going back, which is what their own bank statement will show.
 */
const PaymentRow = ({ payment }: { payment: Payment }) => {
  const isReversal = payment.reversesPaymentId != null;

  return (
    <Stack direction="row" justifyContent="space-between" sx={{ py: 1.25 }}>
      <Box>
        <Typography variant="body2">
          {isReversal ? 'Refund' : payment.packageName}
        </Typography>
        <Typography variant="caption" color="text.secondary">
          {formatDate(payment.paymentDate)} · {payment.paymentMethod}
          {payment.currency === 'Lbp' && ' · paid in LBP'}
        </Typography>
      </Box>
      <Typography
        variant="body2"
        color={isReversal ? 'error.main' : 'text.primary'}
        sx={{ fontWeight: 500 }}
      >
        {formatMoney(payment.amount)}
      </Typography>
    </Stack>
  );
};

export default MyMembershipPage;
