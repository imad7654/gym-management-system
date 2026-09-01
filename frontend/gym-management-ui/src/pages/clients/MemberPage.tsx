import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import {
  Alert,
  Button,
  CircularProgress,
  Container,
  Paper,
  Stack,
  Typography,
} from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import EditIcon from '@mui/icons-material/Edit';
import PauseCircleIcon from '@mui/icons-material/PauseCircle';
import PlayCircleIcon from '@mui/icons-material/PlayCircle';
import RestoreIcon from '@mui/icons-material/Restore';
import { clientService } from '@services/clientService';
import { ClientFormDialog } from '@components/clients';
import { PaymentFormDialog } from '@components/payments';
import {
  MemberStatusHeader,
  MemberMoneyHistory,
  MemberDetails,
} from '@components/members';

/**
 * One member, everything about them, and the things you do to them.
 *
 * The screen the app was missing. Renewing used to mean opening Payments, finding the
 * member in a dropdown, submitting, then searching for them again somewhere else to check
 * the new end date. Here the renewal happens on the page that shows the answer.
 *
 * Ordered for a phone: who they are and whether they can train, then how to reach them,
 * then the actions, then the history. Only the first block is guaranteed to be on screen,
 * so nothing below it is needed to answer "are they paid up?".
 */
const MemberPage = () => {
  const { id } = useParams<{ id: string }>();
  const memberId = Number(id);
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [renewOpen, setRenewOpen] = useState(false);
  const [editOpen, setEditOpen] = useState(false);

  const {
    data: member,
    isLoading,
    isError,
  } = useQuery({
    queryKey: ['member', memberId],
    queryFn: () => clientService.getMemberSummary(memberId),
    enabled: Number.isFinite(memberId),
  });

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ['member', memberId] });
    queryClient.invalidateQueries({ queryKey: ['clients'] });
    queryClient.invalidateQueries({ queryKey: ['dashboard'] });
  };

  const freezeMutation = useMutation({
    mutationFn: (suspend: boolean) =>
      suspend
        ? clientService.suspendClient(memberId)
        : clientService.resumeClient(memberId),
    onSuccess: refresh,
  });

  const restoreMutation = useMutation({
    mutationFn: () => clientService.restoreClient(memberId),
    onSuccess: refresh,
  });

  if (isLoading) {
    return (
      <Container maxWidth="md" sx={{ mt: 4, display: 'flex', justifyContent: 'center' }}>
        <CircularProgress />
      </Container>
    );
  }

  if (isError || !member) {
    return (
      <Container maxWidth="md" sx={{ mt: 4 }}>
        <Alert severity="error">
          That member could not be loaded. They may have been removed permanently.
        </Alert>
        <Button startIcon={<ArrowBackIcon />} onClick={() => navigate('/admin/clients')} sx={{ mt: 2 }}>
          Back to members
        </Button>
      </Container>
    );
  }

  // A removed member still opens, because opening them is how they get restored. Every
  // action that would change their money or dates is hidden while they are removed.
  const removed = !member.isActive;

  return (
    <Container maxWidth="md" sx={{ mt: { xs: 2, sm: 3 }, mb: 6, px: { xs: 1.5, sm: 3 } }}>
      <Button
        startIcon={<ArrowBackIcon />}
        onClick={() => navigate('/admin/clients')}
        sx={{ mb: 1.5 }}
        size="small"
      >
        Members
      </Button>

      {removed && (
        <Alert
          severity="warning"
          sx={{ mb: 2 }}
          action={
            <Button
              size="small"
              startIcon={<RestoreIcon />}
              onClick={() => restoreMutation.mutate()}
              disabled={restoreMutation.isPending}
            >
              Restore
            </Button>
          }
        >
          This member was removed. Their history is kept and nothing is lost.
        </Alert>
      )}

      <MemberStatusHeader member={member} />

      {!removed && (
        <Paper sx={{ p: { xs: 1.5, sm: 2 }, mb: 2 }}>
          <Stack
            direction={{ xs: 'column', sm: 'row' }}
            spacing={1}
            sx={{ '& > *': { flex: 1 } }}
          >
            <Button
              variant="contained"
              size="large"
              onClick={() => setRenewOpen(true)}
            >
              {member.totalOwed > 0 ? 'Take payment' : 'Renew'}
            </Button>
            <Button
              variant="outlined"
              startIcon={member.isSuspended ? <PlayCircleIcon /> : <PauseCircleIcon />}
              onClick={() => freezeMutation.mutate(!member.isSuspended)}
              disabled={freezeMutation.isPending}
            >
              {member.isSuspended ? 'Unfreeze' : 'Freeze'}
            </Button>
            <Button variant="outlined" startIcon={<EditIcon />} onClick={() => setEditOpen(true)}>
              Edit
            </Button>
          </Stack>

          {member.isSuspended && (
            <Typography variant="caption" color="text.secondary" sx={{ mt: 1.5, display: 'block' }}>
              Frozen memberships keep their end date — freezing stops entry, it does not
              hand days back.
            </Typography>
          )}
        </Paper>
      )}

      <MemberMoneyHistory member={member} onChanged={refresh} readOnly={removed} />

      <MemberDetails member={member} />

      {renewOpen && (
        <PaymentFormDialog
          open={renewOpen}
          onClose={() => {
            setRenewOpen(false);
            refresh();
          }}
          lockedClient={{ id: member.id, fullName: member.fullName }}
          defaultPackageId={member.outstanding[0]?.packageId ?? member.currentPackageId ?? undefined}
        />
      )}

      {editOpen && (
        <ClientFormDialog
          open={editOpen}
          onClose={() => {
            setEditOpen(false);
            refresh();
          }}
          client={{
            id: member.id,
            firstName: member.fullName.split(' ')[0] ?? '',
            lastName: member.fullName.split(' ').slice(1).join(' '),
            fullName: member.fullName,
            email: member.email ?? undefined,
            phoneNumber: member.phoneNumber,
            dateOfBirth: member.dateOfBirth ?? undefined,
            gender: (member.gender as never) ?? undefined,
            address: member.address ?? undefined,
            emergencyContact: member.emergencyContact ?? undefined,
            emergencyPhone: member.emergencyPhone ?? undefined,
            notes: member.notes ?? undefined,
            currentPackageId: member.currentPackageId ?? undefined,
            currentPackageName: member.currentPackageName ?? undefined,
            membershipStartDate: member.membershipStartDate ?? undefined,
            membershipEndDate: member.membershipEndDate ?? undefined,
            membershipStatus: member.membershipStatus,
            paymentStatus: 'Paid',
            isActive: member.isActive,
            createdAt: member.createdAt,
            updatedAt: member.createdAt,
          }}
        />
      )}
    </Container>
  );
};

export default MemberPage;
