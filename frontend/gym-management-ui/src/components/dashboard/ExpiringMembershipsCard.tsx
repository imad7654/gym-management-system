import {
  Alert,
  Box,
  Chip,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { dashboardService } from '@services/dashboardService';
import { DASHBOARD_CONFIG } from '@constants/config';

interface ExpiringMembershipsCardProps {
  /** The dashboard's "Expiring Soon" count, used to say when this list is truncated. */
  totalCount?: number;
}

/**
 * Who is about to lapse, and how soon.
 *
 * The dashboard already showed a count of these; this is the list behind it, so the desk
 * can actually pick up the phone. Sorted soonest first by the server, which also caps the
 * list at ten rows - hence the note when there are more people than that to call.
 */
export const ExpiringMembershipsCard = ({
  totalCount,
}: ExpiringMembershipsCardProps) => {
  const days = DASHBOARD_CONFIG.EXPIRING_MEMBERSHIPS_DAYS;
  const maxRows = DASHBOARD_CONFIG.EXPIRING_MEMBERSHIPS_MAX_ROWS;

  const { data: expiring, isLoading, isError } = useQuery({
    queryKey: ['dashboard', 'expiring-memberships', days],
    queryFn: () => dashboardService.getExpiringMemberships(days),
  });

  const shown = expiring?.length ?? 0;
  const isTruncated = shown >= maxRows && (totalCount ?? 0) > shown;

  /**
   * Membership end dates are inclusive, so 0 days left means today is still a valid day
   * to train - the member lapses tomorrow.
   */
  const renderWhen = (daysUntilExpiration: number) => {
    if (daysUntilExpiration <= 0) {
      return <Chip size="small" color="error" label="Last day today" />;
    }
    if (daysUntilExpiration === 1) {
      return <Chip size="small" color="error" label="Last day tomorrow" />;
    }
    return (
      <Chip size="small" color="warning" label={`${daysUntilExpiration} days left`} />
    );
  };

  return (
    <Paper sx={{ p: 3 }}>
      <Typography variant="h6" gutterBottom>
        Expiring in the next {days} days
      </Typography>

      {isLoading ? (
        <Typography variant="body2" color="text.secondary">
          Loading...
        </Typography>
      ) : isError ? (
        // Never fall through to the empty state here. "Everyone is paid up" is the most
        // reassuring thing this card can say, and saying it because the request failed
        // would tell the desk there is nobody to chase when it does not actually know.
        <Alert severity="error">
          Could not load who is expiring. The list may not be empty - please reload.
        </Alert>
      ) : !shown ? (
        <Box sx={{ py: 3, textAlign: 'center' }}>
          <Typography variant="body2" color="text.secondary">
            Nobody is due to expire this week. Everyone is paid up.
          </Typography>
        </Box>
      ) : (
        <>
          <TableContainer sx={{ overflowX: 'auto' }}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Member</TableCell>
                  <TableCell>Phone</TableCell>
                  <TableCell>Package</TableCell>
                  <TableCell>Last day</TableCell>
                  <TableCell>Expires</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {expiring!.map((member) => (
                  <TableRow key={member.clientId} hover>
                    <TableCell>{member.clientName}</TableCell>
                    <TableCell>{member.phoneNumber}</TableCell>
                    <TableCell>{member.packageName}</TableCell>
                    <TableCell>
                      {new Date(member.expirationDate).toLocaleDateString()}
                    </TableCell>
                    <TableCell>{renderWhen(member.daysUntilExpiration)}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>

          {isTruncated && (
            <Typography variant="body2" color="text.secondary" sx={{ mt: 2 }}>
              Showing the {shown} soonest of {totalCount}.
            </Typography>
          )}
        </>
      )}
    </Paper>
  );
};
