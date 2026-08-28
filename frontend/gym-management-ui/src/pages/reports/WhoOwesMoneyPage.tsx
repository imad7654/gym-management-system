import {
  Alert,
  Box,
  Chip,
  CircularProgress,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tooltip,
  Typography,
} from '@mui/material';
import { CheckCircleOutline, Phone } from '@mui/icons-material';
import { useQuery } from '@tanstack/react-query';
import { reportService } from '@services/reportService';
import { OwedAmount } from '@app-types/index';

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

          <TableContainer component={Paper}>
            <Table>
              <TableHead>
                <TableRow>
                  <TableCell>Member</TableCell>
                  <TableCell>Phone</TableCell>
                  <TableCell>Package</TableCell>
                  <TableCell align="right">Paid</TableCell>
                  <TableCell align="right">Still owes</TableCell>
                  <TableCell>Waiting</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {report.members.map((row) => (
                  <OwedRow key={`${row.clientId}-${row.packageName}`} row={row} />
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        </>
      )}
    </Box>
  );
};

const OwedRow = ({ row }: { row: OwedAmount }) => (
  <TableRow hover>
    <TableCell>
      <Typography variant="body2" sx={{ fontWeight: 600 }}>
        {row.clientName}
      </Typography>
      <Chip
        size="small"
        label={row.membershipStatus}
        variant="outlined"
        sx={{ mt: 0.5, height: 20, fontSize: 11 }}
      />
    </TableCell>
    <TableCell>
      {/* A tel: link, because this list exists to be phoned through. */}
      <Stack
        direction="row"
        spacing={0.5}
        alignItems="center"
        component="a"
        href={`tel:${row.phoneNumber.replace(/\s/g, '')}`}
        sx={{ color: 'inherit', textDecoration: 'none', '&:hover': { color: 'primary.main' } }}
      >
        <Phone sx={{ fontSize: 15 }} />
        <span>{row.phoneNumber}</span>
      </Stack>
    </TableCell>
    <TableCell>
      <Tooltip title={`Full price ${usd(row.packagePrice)}`}>
        <span>{row.packageName}</span>
      </Tooltip>
    </TableCell>
    <TableCell align="right">{usd(row.amountPaid)}</TableCell>
    <TableCell align="right" sx={{ fontWeight: 700, color: 'warning.main' }}>
      {usd(row.amountOwed)}
    </TableCell>
    <TableCell>
      <Tooltip title={`First paid ${showDate(row.owingSince)}`}>
        <span>
          {row.daysOutstanding === 0
            ? 'Today'
            : `${row.daysOutstanding} day${row.daysOutstanding === 1 ? '' : 's'}`}
        </span>
      </Tooltip>
    </TableCell>
  </TableRow>
);

export default WhoOwesMoneyPage;
