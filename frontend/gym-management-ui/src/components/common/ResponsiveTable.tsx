import { ReactNode } from 'react';
import {
  Box,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
  useMediaQuery,
  useTheme,
} from '@mui/material';

/**
 * One list, shown as a table on a computer and as cards on a phone.
 *
 * A seven-column table on a 375px screen either scrolls sideways — which hides the columns
 * that matter behind the ones that do not — or squeezes every cell to two words. Cards drop
 * the shared header row and repeat the labels instead, which costs vertical space and buys
 * back readability.
 *
 * Both layouts are driven by the same column definitions, so a column added to the table
 * cannot go missing on the phone.
 */
export interface ResponsiveColumn<T> {
  /** Column heading, and the label beside the value on a phone card. */
  header: string;
  render: (row: T) => ReactNode;
  align?: 'left' | 'right';
  /**
   * Shown large at the top of the phone card with no label — the thing you scan for.
   * Exactly one column per table should be primary.
   */
  primary?: boolean;
  /** Sits beside the primary value rather than in the labelled list. Use for a status chip. */
  badge?: boolean;
  /** Actions column: no label on the card, and it does not trigger the row click. */
  actions?: boolean;
  /** Left off the phone card entirely — detail that does not earn its space there. */
  hideOnPhone?: boolean;
}

interface ResponsiveTableProps<T> {
  columns: ResponsiveColumn<T>[];
  rows: T[];
  rowKey: (row: T) => string | number;
  onRowClick?: (row: T) => void;
  isLoading?: boolean;
  emptyMessage?: string;
}

export function ResponsiveTable<T>({
  columns,
  rows,
  rowKey,
  onRowClick,
  isLoading = false,
  emptyMessage = 'Nothing to show',
}: ResponsiveTableProps<T>) {
  const theme = useTheme();
  const isPhone = useMediaQuery(theme.breakpoints.down('sm'));

  if (isLoading) {
    return (
      <Paper sx={{ p: 3, textAlign: 'center' }}>
        <Typography color="text.secondary">Loading…</Typography>
      </Paper>
    );
  }

  if (rows.length === 0) {
    return (
      <Paper sx={{ p: 3, textAlign: 'center' }}>
        <Typography color="text.secondary">{emptyMessage}</Typography>
      </Paper>
    );
  }

  if (isPhone) {
    const primary = columns.find((c) => c.primary);
    const badges = columns.filter((c) => c.badge);
    const actions = columns.filter((c) => c.actions);
    const details = columns.filter(
      (c) => !c.primary && !c.badge && !c.actions && !c.hideOnPhone
    );

    return (
      <Stack spacing={1}>
        {rows.map((row) => (
          <Paper
            key={rowKey(row)}
            onClick={onRowClick ? () => onRowClick(row) : undefined}
            sx={{
              p: 1.5,
              cursor: onRowClick ? 'pointer' : 'default',
              '&:active': onRowClick ? { bgcolor: 'action.selected' } : undefined,
            }}
          >
            <Box
              sx={{
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: 'flex-start',
                gap: 1,
                mb: details.length ? 1 : 0,
              }}
            >
              <Typography variant="subtitle1" sx={{ fontWeight: 500, minWidth: 0 }}>
                {primary ? primary.render(row) : null}
              </Typography>
              <Box sx={{ display: 'flex', gap: 0.5, flexShrink: 0, alignItems: 'center' }}>
                {badges.map((c) => (
                  <Box key={c.header}>{c.render(row)}</Box>
                ))}
              </Box>
            </Box>

            {details.map((c) => (
              <Box
                key={c.header}
                sx={{ display: 'flex', justifyContent: 'space-between', gap: 2, py: 0.25 }}
              >
                <Typography variant="body2" color="text.secondary">
                  {c.header}
                </Typography>
                <Typography variant="body2" sx={{ textAlign: 'right', minWidth: 0 }}>
                  {c.render(row)}
                </Typography>
              </Box>
            ))}

            {actions.length > 0 && (
              <Box
                sx={{ display: 'flex', justifyContent: 'flex-end', gap: 0.5, mt: 1 }}
                onClick={(e) => e.stopPropagation()}
              >
                {actions.map((c) => (
                  <Box key={c.header}>{c.render(row)}</Box>
                ))}
              </Box>
            )}
          </Paper>
        ))}
      </Stack>
    );
  }

  return (
    <TableContainer component={Paper}>
      <Table>
        <TableHead>
          <TableRow>
            {columns.map((c) => (
              <TableCell key={c.header} align={c.align ?? (c.actions ? 'right' : 'left')}>
                {c.actions ? '' : c.header}
              </TableCell>
            ))}
          </TableRow>
        </TableHead>
        <TableBody>
          {rows.map((row) => (
            <TableRow
              key={rowKey(row)}
              hover={!!onRowClick}
              onClick={onRowClick ? () => onRowClick(row) : undefined}
              sx={{ cursor: onRowClick ? 'pointer' : 'default' }}
            >
              {columns.map((c) => (
                <TableCell
                  key={c.header}
                  align={c.align ?? (c.actions ? 'right' : 'left')}
                  onClick={c.actions ? (e) => e.stopPropagation() : undefined}
                >
                  {c.render(row)}
                </TableCell>
              ))}
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
