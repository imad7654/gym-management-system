import { useState } from 'react';
import {
  Alert,
  Box,
  Chip,
  CircularProgress,
  MenuItem,
  Pagination,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { useQuery, keepPreviousData } from '@tanstack/react-query';
import { reportService } from '@services/reportService';
import { AuditActionString, AuditEntry } from '@app-types/index';
import { ResponsiveTable } from '@components/common';

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
            <ResponsiveTable<AuditEntry>
              rows={data.items}
              rowKey={(entry) => entry.id}
              emptyMessage="Nothing recorded yet for this filter."
              columns={[
                {
                  header: 'Happened',
                  primary: true,
                  render: (e) => (
                    <>
                      <Typography variant="body2">{e.summary}</Typography>
                      {e.details && (
                        <Typography variant="caption" color="text.secondary">
                          {e.details}
                        </Typography>
                      )}
                    </>
                  ),
                },
                {
                  header: 'What',
                  badge: true,
                  render: (e) => (
                    <Chip
                      size="small"
                      label={e.action}
                      color={ACTION_COLOUR[e.action] ?? 'default'}
                      variant="outlined"
                    />
                  ),
                },
                {
                  header: 'When',
                  render: (e) => (
                    <Box component="span" sx={{ whiteSpace: 'nowrap', color: 'text.secondary' }}>
                      {showWhen(e.occurredAt)}
                    </Box>
                  ),
                },
                {
                  header: 'Who',
                  // Null means nobody signed in did it — a nightly job, or a seeded record.
                  render: (e) =>
                    e.actorName ?? (
                      <Typography variant="body2" color="text.secondary" component="span">
                        System
                      </Typography>
                    ),
                },
              ]}
            />

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

export default AuditTrailPage;
