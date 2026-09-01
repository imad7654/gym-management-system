import { Box, Paper, Typography } from '@mui/material';
import { MemberSummary } from '@app-types/index';

/**
 * The things you look up occasionally rather than every visit — join date, emergency
 * contact, notes. Deliberately last on the page: none of it answers "can they train
 * today", so none of it earns space above the payment history.
 */
interface MemberDetailsProps {
  member: MemberSummary;
}

const formatDate = (value?: string | null) =>
  value
    ? new Date(value).toLocaleDateString(undefined, {
        day: 'numeric',
        month: 'short',
        year: 'numeric',
      })
    : null;

export const MemberDetails = ({ member }: MemberDetailsProps) => {
  const rows: Array<[string, string | null]> = [
    ['Member since', formatDate(member.membershipStartDate ?? member.createdAt)],
    ['Email', member.email ?? null],
    ['Date of birth', formatDate(member.dateOfBirth)],
    ['Gender', member.gender ?? null],
    ['Address', member.address ?? null],
    [
      'Emergency contact',
      member.emergencyContact
        ? `${member.emergencyContact}${member.emergencyPhone ? ` — ${member.emergencyPhone}` : ''}`
        : null,
    ],
    ['Notes', member.notes ?? null],
  ];

  const filled = rows.filter(([, value]) => value);

  if (filled.length === 0) return null;

  return (
    <Paper sx={{ p: { xs: 1.5, sm: 2 } }}>
      <Typography variant="h6" sx={{ mb: 1.5 }}>
        Details
      </Typography>

      <Box
        sx={{
          display: 'grid',
          gridTemplateColumns: { xs: '1fr', sm: 'minmax(9rem, auto) 1fr' },
          rowGap: 1,
          columnGap: 2,
        }}
      >
        {filled.map(([label, value]) => (
          <Box key={label} sx={{ display: 'contents' }}>
            <Typography variant="body2" color="text.secondary">
              {label}
            </Typography>
            <Typography variant="body2" sx={{ mb: { xs: 1, sm: 0 }, wordBreak: 'break-word' }}>
              {value}
            </Typography>
          </Box>
        ))}
      </Box>
    </Paper>
  );
};
