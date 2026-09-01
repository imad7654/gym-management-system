import { useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Chip,
  Container,
  IconButton,
  Snackbar,
  Tooltip,
  Typography,
} from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import AddIcon from '@mui/icons-material/Add';
import EditIcon from '@mui/icons-material/Edit';
import KeyIcon from '@mui/icons-material/Key';
import BlockIcon from '@mui/icons-material/Block';
import RestoreIcon from '@mui/icons-material/Restore';
import { userService } from '@services/userService';
import { ResponsiveTable, type ResponsiveColumn } from '@components/common';
import { UserFormDialog, ResetPasswordDialog } from '@components/users';
import type { UserAccount } from '@app-types/index';
import { describeApiError } from '@lib/errors';

/**
 * The accounts that can sign in and run the gym.
 *
 * This screen exists to stop one specific disaster: a single administrator account with a
 * forgotten password and no way back in. So the two things it makes easy are adding a
 * second administrator and resetting somebody's password.
 *
 * The rules are enforced on the server, not here. This page greys out what will be refused
 * so nobody clicks it, but the server refuses it either way.
 */
const UsersPage = () => {
  const queryClient = useQueryClient();

  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<UserAccount | null>(null);
  const [resetting, setResetting] = useState<UserAccount | null>(null);
  const [toast, setToast] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const { data: users, isLoading } = useQuery({
    queryKey: ['users'],
    queryFn: () => userService.getUsers(),
  });

  const setActive = useMutation({
    mutationFn: ({ user, active }: { user: UserAccount; active: boolean }) =>
      active ? userService.restoreUser(user.id) : userService.deactivateUser(user.id),
    onSuccess: (_data, { user, active }) => {
      queryClient.invalidateQueries({ queryKey: ['users'] });
      setToast(
        active
          ? `${user.fullName} can sign in again`
          : `${user.fullName} can no longer sign in`
      );
    },
    onError: (err) => setError(describeApiError(err)),
  });

  const openAdd = () => {
    setEditing(null);
    setFormOpen(true);
  };

  const openEdit = (user: UserAccount) => {
    setEditing(user);
    setFormOpen(true);
  };

  const activeAdmins = (users ?? []).filter((u) => u.isActive).length;

  const columns: ResponsiveColumn<UserAccount>[] = [
    {
      header: 'Name',
      primary: true,
      render: (u) => u.fullName,
    },
    {
      header: 'Status',
      badge: true,
      render: (u) =>
        !u.isActive ? (
          <Chip label="Switched off" size="small" />
        ) : u.isYou ? (
          <Chip label="You" size="small" color="primary" />
        ) : (
          <Chip label="Can sign in" size="small" color="success" variant="outlined" />
        ),
    },
    {
      header: 'Email',
      render: (u) => u.email,
    },
    {
      header: 'Phone',
      hideOnPhone: true,
      render: (u) => u.phoneNumber || '—',
    },
    {
      header: 'Actions',
      actions: true,
      align: 'right',
      render: (u) => {
        // The server refuses both of these. Saying why up front beats letting reception
        // click a button and read an error.
        const cannotSwitchOff = u.isYou
          ? 'You cannot switch off your own account'
          : u.isLastAdmin
            ? 'The only administrator left — add another first'
            : null;

        return (
          <Box sx={{ display: 'flex', gap: 0.5, justifyContent: 'flex-end' }}>
            <Tooltip title="Edit">
              <IconButton size="small" onClick={() => openEdit(u)} aria-label={`Edit ${u.fullName}`}>
                <EditIcon fontSize="small" />
              </IconButton>
            </Tooltip>

            <Tooltip title="Reset their password">
              <IconButton
                size="small"
                onClick={() => setResetting(u)}
                aria-label={`Reset password for ${u.fullName}`}
              >
                <KeyIcon fontSize="small" />
              </IconButton>
            </Tooltip>

            {u.isActive ? (
              <Tooltip title={cannotSwitchOff ?? 'Stop this account signing in'}>
                <span>
                  <IconButton
                    size="small"
                    color="error"
                    disabled={cannotSwitchOff !== null}
                    onClick={() => setActive.mutate({ user: u, active: false })}
                    aria-label={`Switch off ${u.fullName}`}
                  >
                    <BlockIcon fontSize="small" />
                  </IconButton>
                </span>
              </Tooltip>
            ) : (
              <Tooltip title="Let this account sign in again">
                <IconButton
                  size="small"
                  color="success"
                  onClick={() => setActive.mutate({ user: u, active: true })}
                  aria-label={`Switch on ${u.fullName}`}
                >
                  <RestoreIcon fontSize="small" />
                </IconButton>
              </Tooltip>
            )}
          </Box>
        );
      },
    },
  ];

  return (
    <Container maxWidth="lg" sx={{ mt: 4, mb: 4 }}>
      <Box
        sx={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          gap: 2,
          flexWrap: 'wrap',
          mb: 1,
        }}
      >
        <Typography variant="h4">Who can sign in</Typography>
        <Button variant="contained" startIcon={<AddIcon />} onClick={openAdd}>
          Add administrator
        </Button>
      </Box>

      <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
        These are the people who run the gym, not the members.
      </Typography>

      {/*
        The warning that matters. One account and a forgotten password means nobody can get
        in - there is no email in this system, so there is no reset link to fall back on.
      */}
      {!isLoading && activeAdmins === 1 && (
        <Alert severity="warning" sx={{ mb: 3 }}>
          There is only one account that can sign in. If that password is forgotten, nobody
          can get into the system — there is no reset email to fall back on. Add a second
          administrator you trust.
        </Alert>
      )}

      {error && (
        <Alert severity="error" sx={{ mb: 3 }} onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      <ResponsiveTable
        columns={columns}
        rows={users ?? []}
        rowKey={(u) => u.id}
        isLoading={isLoading}
        emptyMessage="No accounts yet"
      />

      <UserFormDialog
        open={formOpen}
        onClose={() => setFormOpen(false)}
        user={editing}
        onSaved={setToast}
      />

      <ResetPasswordDialog
        open={resetting !== null}
        onClose={() => setResetting(null)}
        user={resetting}
        onDone={setToast}
      />

      <Snackbar
        open={toast !== null}
        autoHideDuration={5000}
        onClose={() => setToast(null)}
        message={toast ?? ''}
      />
    </Container>
  );
};

export default UsersPage;
