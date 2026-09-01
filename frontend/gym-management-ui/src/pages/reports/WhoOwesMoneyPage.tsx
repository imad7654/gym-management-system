import {
  Alert,
  Box,
  Chip,
  CircularProgress,
  Paper,
  Stack,
  Tooltip,
  Typography,
} from '@mui/material';
import { CheckCircleOutline, Phone } from '@mui/icons-material';
import { useQuery } from '@tanstack/react-query';
import { reportService } from '@services/reportService';
import { OwedAmount } from '@app-types/index';
import { ResponsiveTable } from '@components/common';
import { useNavigate } from 'react-router-dom';

const usd = (amount: number) =>
  `$${amount.toLocaleString('en-US', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}`;

const showDate = (iso: string) => {
  const date = new Date(iso);
  return Number.isNaN(date.getTime())
    ? iso
    : date.toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' });
};

/**
 * Who owes money.
 *
 * A member who pays less than the package price has the money recorded but does not get
 * the time — otherwise half payments quietly unlock full months and the gym loses track of
 * its income. This is the list that makes that rule collectable instead of just strict.
 *
 * The owner works it by phoning people, so it is sorted by how long the money has been
 * outstanding rather than by size: the oldest debt is the one least likely to be paid.
 */
const WhoOwesMoneyPage = () => {
  const navigate = useNavigate();
  const { data, isLoading, isError } = useQuery({
    queryKey: ['reports', 'who-owes'],
    queryFn: reportService.getWhoOwesMoney,
  });

  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', mt: 6 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (isError) {
    return (
      <Alert severity="error" sx={{ mt: 2 }}>
        Could not load the list. Check that the API is running, then reload this page.
      </Alert>
    );
  }

  const report = data!;

  return (
    <Box sx={{ maxWidth: 1000 }}>
      <Typography variant="h4" gutterBottom sx={{ fontWeight: 700 }}>
        Who owes money
      </Typography>
      <Typography variant="body1" color="text.secondary" sx={{ mb: 3 }}>
        Members who paid part of a package and still owe the rest. Their membership does not
        start until it is paid in full.
      </Typography>

      {report.memberCount === 0 ? (
        <Paper sx={{ p: 4, textAlign: 'center' }}>
          <CheckCircleOutline color="success" sx={{ fontSize: 44, mb: 1 }} />
          <Typography variant="h6" sx={{ fontWeight: 600 }}>
            Nobody owes anything
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Every member who has paid something has paid in full.
          </Typography>
        </Paper>
      ) : (
        <>
          <Paper
            variant="outlined"
            sx={{ px: 3, py: 2, mb: 3, display: 'inline-flex', alignItems: 'baseline', gap: 1.5 }}
          >
            <Typography variant="h4" sx={{ fontWeight: 700, color: 'warning.main' }}>
              {usd(report.totalOwed)}
            </Typography>
            <Typography variant="body1" color="text.secondary">
              owed by {report.memberCount}{' '}
              {report.memberCount === 1 ? 'member' : 'members'}
            </Typography>
          </Paper>

          <ResponsiveTable<OwedAmount>
            rows={report.members}
            rowKey={(row) => `${row.clientId}-${row.packageName}`}
            onRowClick={(row) => navigate(`/admin/clients/${row.clientId}`)}
            emptyMessage="Nobody owes anything"
            columns={[
              { header: 'Member', primary: true, render: (r) => r.clientName },
              {
                header: 'Status',
                badge: true,
                render: (r) => (
                  <Chip
                    size="small"
                    label={r.membershipStatus}
                    variant="outlined"
                    sx={{ height: 20, fontSize: 11 }}
                  />
                ),
              },
              {
                header: 'Still owes',
                align: 'right',
                render: (r) => (
                  <Typography
                    component="span"
                    variant="body2"
                    sx={{ fontWeight: 700, color: 'warning.main' }}
                  >
                    {usd(r.amountOwed)}
                  </Typography>
                ),
              },
              {
                header: 'Phone',
                // A tel: link, because this list exists to be phoned through. It stops the
                // row click so tapping the number rings rather than opening the member.
                render: (r) => (
                  <Stack
                    direction="row"
                    spacing={0.5}
                    alignItems="center"
                    component="a"
                    href={`tel:${r.phoneNumber.replace(/\s/g, '')}`}
                    onClick={(e: React.MouseEvent) => e.stopPropagation()}
                    sx={{
                      color: 'inherit',
                      textDecoration: 'none',
                      justifyContent: 'flex-end',
                      '&:hover': { color: 'primary.main' },
                    }}
                  >
                    <Phone sx={{ fontSize: 15 }} />
                    <span>{r.phoneNumber}</span>
                  </Stack>
                ),
              },
              {
                header: 'Package',
                render: (r) => (
                  <Tooltip title={`Full price ${usd(r.packagePrice)}`}>
                    <span>{r.packageName}</span>
                  </Tooltip>
                ),
              },
              { header: 'Paid', align: 'right', render: (r) => usd(r.amountPaid) },
              {
                header: 'Waiting',
                render: (r) => (
                  <Tooltip title={`First paid ${showDate(r.owingSince)}`}>
                    <span>
                      {r.daysOutstanding === 0
                        ? 'Today'
                        : `${r.daysOutstanding} day${r.daysOutstanding === 1 ? '' : 's'}`}
                    </span>
                  </Tooltip>
                ),
              },
            ]}
          />
        </>
      )}
    </Box>
  );
};

export default WhoOwesMoneyPage;
