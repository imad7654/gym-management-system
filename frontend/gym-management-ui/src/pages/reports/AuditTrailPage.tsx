import { useState } from 'react';
import {
  Alert,
  Box,
  Chip,
  CircularProgress,
  MenuItem,
  Pagination,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from '@mui/material';
import { useQuery, keepPreviousData } from '@tanstack/react-query';
import { reportService } from '@services/reportService';
import { AuditActionString, AuditEntry } from '@app-types/index';

const ACTION_COLOUR: Record<
  AuditActionString,
  'success' | 'info' | 'error' | 'warning' | 'default'
> = {
  Created: 'success',
  Updated: 'info',
  Deleted: 'error',
  Restored: 'success',
  Reversed: 'warning',
  Imported: 'info',
};

const ENTITY_TYPES = ['Client', 'Payment', 'ExchangeRate'];

const showWhen = (iso: string) =>
  new Date(iso).toLocaleString('en-GB', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });

/**
 * Who did what.
 *
 * Recorded from the first day even though the gym starts with a single login, because an
 * audit trail that begins the day you need it is no use at all. What it is really for is
 * the two arguments a gym has: where did this money go, and who moved this member's dates.
 */
const AuditTrailPage = () => {
  const [page, setPage] = useState(1);
  const [entityType, setEntityType] = useState('');
  const [search, setSearch] = useState('');

  const { data, isLoading, isError } = useQuery({
    queryKey: ['reports', 'audit', page, entityType, search],
    queryFn: () =>
      reportService.getAuditTrail({
        page,
        pageSize: 25,
        entityType: entityType || undefined,
        search: search || undefined,
      }),
    // Keeps the table on screen while the next page loads, instead of collapsing to a
    // spinner and throwing away the reader's place.
    placeholderData: keepPreviousData,
  });

  const resetTo = (change: () => void) => {
    change();
    setPage(1);
  };

  return (
    <Box sx={{ maxWidth: 1100 }}>
      <Typography variant="h4" gutterBottom sx={{ fontWeight: 700 }}>
        History
      </Typography>
      <Typography variant="body1" color="text.secondary" sx={{ mb: 3 }}>
        Every payment, refund, membership change and removal — who did it, and when.
      </Typography>

      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ mb: 2 }}>
        <TextField
          select
          size="small"
          label="Show"
          value={entityType}
          onChange={(e) => resetTo(() => setEntityType(e.target.value))}
          sx={{ minWidth: 190 }}
        >
          <MenuItem value="">Everything</MenuItem>
          {ENTITY_TYPES.map((type) => (
            <MenuItem key={type} value={type}>
              {type === 'ExchangeRate' ? 'Exchange rate' : type}
            </MenuItem>
          ))}
        </TextField>
        <TextField
          size="small"
          label="Search"
          placeholder="Member name, or who did it"
          value={search}
          onChange={(e) => resetTo(() => setSearch(e.target.value))}
          sx={{ minWidth: 280 }}
        />
      </Stack>

      {isError && (
        <Alert severity="error">
          Could not load the history. Check that the API is running, then reload this page.
        </Alert>
      )}

      {isLoading && !data ? (
        <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}>
          <CircularProgress />
        </Box>
      ) : (
        data && (
          <>
            <TableContainer component={Paper}>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell sx={{ width: 170 }}>When</TableCell>
                    <TableCell sx={{ width: 110 }}>What</TableCell>
                    <TableCell>Happened</TableCell>
                    <TableCell sx={{ width: 170 }}>Who</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {data.items.map((entry) => (
                    <AuditRow key={entry.id} entry={entry} />
                  ))}
                  {data.items.length === 0 && (
                    <TableRow>
                      <TableCell colSpan={4} align="center" sx={{ py: 4 }}>
                        <Typography variant="body2" color="text.secondary">
                          Nothing recorded yet for this filter.
                        </Typography>
                      </TableCell>
                    </TableRow>
                  )}
                </TableBody>
              </Table>
            </TableContainer>

            {data.totalPages > 1 && (
              <Stack alignItems="center" sx={{ mt: 2 }}>
                <Pagination
                  count={data.totalPages}
                  page={data.page}
                  onChange={(_, value) => setPage(value)}
                  color="primary"
                />
              </Stack>
            )}
          </>
        )
      )}
    </Box>
  );
};

const AuditRow = ({ entry }: { entry: AuditEntry }) => (
  <TableRow hover>
    <TableCell sx={{ whiteSpace: 'nowrap', color: 'text.secondary' }}>
      {showWhen(entry.occurredAt)}
    </TableCell>
    <TableCell>
      <Chip
        size="small"
        label={entry.action}
        color={ACTION_COLOUR[entry.action] ?? 'default'}
        variant="outlined"
      />
    </TableCell>
    <TableCell>
      <Typography variant="body2">{entry.summary}</Typography>
      {entry.details && (
        <Typography variant="caption" color="text.secondary">
          {entry.details}
        </Typography>
      )}
    </TableCell>
    <TableCell>
      {/* Null means nobody signed in did it — a nightly job, or a seeded record. */}
      {entry.actorName ?? (
        <Typography variant="body2" color="text.secondary" component="span">
          System
        </Typography>
      )}
    </TableCell>
  </TableRow>
);

export default AuditTrailPage;
