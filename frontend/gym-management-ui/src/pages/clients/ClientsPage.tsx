import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Container,
  Typography,
  Box,
  Button,
  Paper,
  Chip,
  ChipProps,
  IconButton,
  TextField,
  InputAdornment,
} from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { clientService } from '@services/clientService';
import AddIcon from '@mui/icons-material/Add';
import EditIcon from '@mui/icons-material/Edit';
import DeleteIcon from '@mui/icons-material/Delete';
import SearchIcon from '@mui/icons-material/Search';
import type { ClientQueryParams, Client, ClientListItem } from '../../types/index';
import { ClientFormDialog, DeleteClientDialog } from '@components/clients';
import { ResponsiveTable } from '@components/common';

const ClientsPage = () => {
  const navigate = useNavigate();
  const [queryParams, setQueryParams] = useState<ClientQueryParams>({
    page: 1,
    pageSize: 10,
    search: '',
  });
  const [openDialog, setOpenDialog] = useState(false);
  const [selectedClient, setSelectedClient] = useState<Client | null>(null);
  const [openDeleteDialog, setOpenDeleteDialog] = useState(false);
  const [clientToDelete, setClientToDelete] = useState<ClientListItem | null>(null);

  const { data: clientsData, isLoading } = useQuery({
    queryKey: ['clients', queryParams],
    queryFn: () => clientService.getClients(queryParams),
  });

  const handleAddClient = () => {
    setSelectedClient(null);
    setOpenDialog(true);
  };

  const handleEditClient = async (clientListItem: ClientListItem) => {
    // Fetch full client details for editing
    const fullClient = await clientService.getClient(clientListItem.id);
    setSelectedClient(fullClient);
    setOpenDialog(true);
  };

  const handleDeleteClient = (client: ClientListItem) => {
    setClientToDelete(client);
    setOpenDeleteDialog(true);
  };

  const handleCloseDialog = () => {
    setOpenDialog(false);
    setSelectedClient(null);
  };

  const handleCloseDeleteDialog = () => {
    setOpenDeleteDialog(false);
    setClientToDelete(null);
  };

  const getStatusColor = (status: string): ChipProps['color'] => {
    switch (status) {
      case 'Active':
        return 'success';
      case 'Expired':
        return 'error';
      case 'Pending':
        return 'warning';
      default:
        return 'default';
    }
  };

  const getPaymentColor = (status: string): ChipProps['color'] => {
    switch (status) {
      case 'Paid':
        return 'success';
      case 'Pending':
        return 'warning';
      case 'Overdue':
        return 'error';
      default:
        return 'default';
    }
  };

  return (
    <Container maxWidth="lg" sx={{ mt: 4, mb: 4 }}>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 3 }}>
        <Typography variant="h4">Clients</Typography>
        <Button
          variant="contained"
          startIcon={<AddIcon />}
          onClick={handleAddClient}
        >
          Add Client
        </Button>
      </Box>

      <Paper sx={{ p: 2, mb: 2 }}>
        <TextField
          fullWidth
          placeholder="Search by name, phone, or email..."
          value={queryParams.search}
          onChange={(e) =>
            setQueryParams({ ...queryParams, search: e.target.value, page: 1 })
          }
          InputProps={{
            startAdornment: (
              <InputAdornment position="start">
                <SearchIcon />
              </InputAdornment>
            ),
          }}
        />
      </Paper>

      <ResponsiveTable<ClientListItem>
        rows={clientsData?.items ?? []}
        rowKey={(client) => client.id}
        isLoading={isLoading}
        emptyMessage={
          queryParams.search ? 'Nobody matches that search' : 'No members yet'
        }
        onRowClick={(client) => navigate(`/admin/clients/${client.id}`)}
        columns={[
          { header: 'Name', primary: true, render: (c) => c.fullName },
          {
            header: 'Status',
            badge: true,
            render: (c) => (
              <Chip
                label={c.membershipStatus}
                color={getStatusColor(c.membershipStatus)}
                size="small"
              />
            ),
          },
          { header: 'Phone', render: (c) => c.phoneNumber },
          { header: 'Package', render: (c) => c.currentPackageName || '—' },
          {
            header: 'Expires',
            render: (c) =>
              c.membershipEndDate
                ? new Date(c.membershipEndDate).toLocaleDateString()
                : '—',
          },
          {
            header: 'Payment',
            render: (c) => (
              <Chip
                label={c.paymentStatus}
                color={getPaymentColor(c.paymentStatus)}
                size="small"
              />
            ),
          },
          {
            header: 'Actions',
            actions: true,
            render: (c) => (
              <>
                <IconButton
                  size="small"
                  color="primary"
                  aria-label={`Edit ${c.fullName}`}
                  onClick={() => handleEditClient(c)}
                >
                  <EditIcon />
                </IconButton>
                <IconButton
                  size="small"
                  color="error"
                  aria-label={`Remove ${c.fullName}`}
                  onClick={() => handleDeleteClient(c)}
                >
                  <DeleteIcon />
                </IconButton>
              </>
            ),
          },
        ]}
      />

      <Box sx={{ mt: 2, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="body2" color="text.secondary">
          Showing {clientsData?.items.length || 0} of {clientsData?.totalCount || 0} clients
        </Typography>
        <Box>
          <Button
            disabled={!clientsData?.hasPreviousPage}
            onClick={() => setQueryParams({ ...queryParams, page: queryParams.page! - 1 })}
          >
            Previous
          </Button>
          <Button
            disabled={!clientsData?.hasNextPage}
            onClick={() => setQueryParams({ ...queryParams, page: queryParams.page! + 1 })}
          >
            Next
          </Button>
        </Box>
      </Box>

      <ClientFormDialog
        open={openDialog}
        onClose={handleCloseDialog}
        client={selectedClient}
      />

      <DeleteClientDialog
        open={openDeleteDialog}
        onClose={handleCloseDeleteDialog}
        client={clientToDelete ? { id: clientToDelete.id, fullName: clientToDelete.fullName } : null}
      />
    </Container>
  );
};

export default ClientsPage;
