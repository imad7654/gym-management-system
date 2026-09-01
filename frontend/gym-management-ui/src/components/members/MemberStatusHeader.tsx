import { Box, Button, Chip, ChipProps, Paper, Stack, Typography } from '@mui/material';
import CallIcon from '@mui/icons-material/Call';
import WhatsAppIcon from '@mui/icons-material/WhatsApp';
import { MemberSummary, MembershipStatusString } from '@app-types/index';
import { contactLinks } from '@lib/contact';

/**
 * The only part of the member page guaranteed to be on screen on a phone.
 *
 * It answers the two questions reception has with a member standing in front of them:
 * can this person train, and how do I reach them. Everything else on the page is below
 * this and can be scrolled to.
 */
interface MemberStatusHeaderProps {
  member: MemberSummary;
}

const statusColor = (status: MembershipStatusString): ChipProps['color'] => {
  switch (status) {
    case 'Active':
      return 'success';
    case 'Expiring':
      return 'warning';
    case 'Expired':
      return 'error';
    case 'Suspended':
      return 'default';
    default:
      return 'default';
  }
};

const formatDate = (value?: string | null) =>
  value
    ? new Date(value).toLocaleDateString(undefined, {
        day: 'numeric',
        month: 'short',
        year: 'numeric',
      })
    : null;

/**
 * Says how long is left in the words a person would use, not as a raw number. "Ends today"
 * and "expired 12 days ago" are both immediately actionable; "0 days" and "-12 days" are
 * things reception has to stop and interpret.
 */
const describeRemaining = (member: MemberSummary): string => {
  if (member.membershipStatus === 'Pending' && !member.membershipEndDate) {
    return 'Has never paid';
  }

  const end = formatDate(member.membershipEndDate);
  const days = member.daysRemaining;

  if (days === null || days === undefined) return end ? `Runs to ${end}` : 'No membership dates';

  if (days < 0) {
    const ago = Math.abs(days);
    return `Expired ${ago === 1 ? 'yesterday' : `${ago} days ago`}${end ? ` — ${end}` : ''}`;
  }

  if (days === 0) return `Last day — ends today${end ? ` (${end})` : ''}`;

  return `${days} ${days === 1 ? 'day' : 'days'} left — to ${end}`;
};

export const MemberStatusHeader = ({ member }: MemberStatusHeaderProps) => {
  const links = contactLinks(member.phoneDigits);

  return (
    <Paper sx={{ p: { xs: 2, sm: 2.5 }, mb: 2 }}>
      <Stack
        direction="row"
        spacing={1}
        alignItems="flex-start"
        justifyContent="space-between"
        sx={{ mb: 0.5 }}
      >
        <Typography variant="h5" sx={{ fontWeight: 600, wordBreak: 'break-word' }}>
          {member.fullName}
        </Typography>
        <Chip
          label={member.membershipStatus}
          color={statusColor(member.membershipStatus)}
          sx={{ flexShrink: 0 }}
        />
      </Stack>

      <Typography
        variant="body1"
        color={member.membershipStatus === 'Expired' ? 'error.main' : 'text.secondary'}
        sx={{ mb: 1 }}
      >
        {describeRemaining(member)}
      </Typography>

      {member.currentPackageName && (
        <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
          {member.currentPackageName}
        </Typography>
      )}

      {member.totalOwed > 0 && (
        <Typography variant="body2" color="warning.main" sx={{ mb: 1.5, fontWeight: 500 }}>
          Owes ${member.totalOwed.toFixed(2)}
        </Typography>
      )}

      <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', flexWrap: 'wrap' }}>
        <Typography variant="body2" sx={{ mr: 0.5 }}>
          {member.phoneNumber}
        </Typography>

        {links && (
          <>
            <Button
              size="small"
              variant="outlined"
              startIcon={<CallIcon />}
              href={links.tel}
            >
              Call
            </Button>
            <Button
              size="small"
              variant="outlined"
              startIcon={<WhatsAppIcon />}
              href={links.whatsapp}
              target="_blank"
              rel="noopener noreferrer"
            >
              WhatsApp
            </Button>
          </>
        )}
      </Box>
    </Paper>
  );
};
