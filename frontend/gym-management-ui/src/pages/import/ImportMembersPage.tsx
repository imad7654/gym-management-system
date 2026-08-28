import { useMemo, useRef, useState } from 'react';
import {
  Alert,
  AlertTitle,
  Box,
  Button,
  Checkbox,
  Chip,
  CircularProgress,
  Divider,
  FormControlLabel,
  Link,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  ToggleButton,
  ToggleButtonGroup,
  Tooltip,
  Typography,
} from '@mui/material';
import {
  CheckCircleOutline,
  CloudUpload,
  Download,
  ErrorOutline,
  ContentCopy,
  Refresh,
} from '@mui/icons-material';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { clientImportService } from '@services/clientImportService';
import {
  MemberImportPreview,
  MemberImportResult,
  MemberImportRow,
  MemberImportRowStatus,
} from '@app-types/index';

type RowFilter = 'all' | 'ready' | 'duplicate' | 'error';

const STATUS_STYLE: Record<
  MemberImportRowStatus,
  { label: string; color: 'success' | 'warning' | 'error' }
> = {
  Ready: { label: 'Will be added', color: 'success' },
  Duplicate: { label: 'Already a member', color: 'warning' },
  Error: { label: 'Needs fixing', color: 'error' },
};

/** Spelled out with the month in words, so nobody has to guess the day from the month. */
const showDate = (iso?: string) => {
  if (!iso) return '—';
  const date = new Date(iso);
  return Number.isNaN(date.getTime())
    ? iso
    : date.toLocaleDateString('en-GB', {
        day: 'numeric',
        month: 'long',
        year: 'numeric',
      });
};

const apiMessage = (error: unknown, fallback: string) =>
  (error as { response?: { data?: { message?: string } } } | null)?.response?.data
    ?.message ?? fallback;

/**
 * Import the gym's existing members (blueprint 6.3).
 *
 * This is the screen that lets The Fit Bear go live on the system instead of a notebook,
 * and it is used properly once. The shape follows from that: check the file, look at what
 * would happen, fix the file, check again - and only then write anything. The owner is
 * about to load their whole real member list, and there is no undo for half of it.
 */
const ImportMembersPage = () => {
  const queryClient = useQueryClient();
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<MemberImportPreview | null>(null);
  const [result, setResult] = useState<MemberImportResult | null>(null);
  const [acknowledged, setAcknowledged] = useState(false);
  const [filter, setFilter] = useState<RowFilter>('all');
  const [dragging, setDragging] = useState(false);

  const previewMutation = useMutation({
    mutationFn: clientImportService.preview,
    onSuccess: (data) => {
      setPreview(data);
      setAcknowledged(false);
      // Land on whatever needs attention: if rows failed, that is what to look at first.
      setFilter(data.errorCount > 0 ? 'error' : 'all');
    },
  });

  const commitMutation = useMutation({
    mutationFn: ({
      file: toImport,
      fileHash,
      acknowledgeSkipped,
    }: {
      file: File;
      fileHash: string;
      acknowledgeSkipped: boolean;
    }) => clientImportService.commit(toImport, fileHash, acknowledgeSkipped),
    onSuccess: (data) => {
      setResult(data);
      setPreview(null);
      setFile(null);
      // The member list has changed under every screen that shows it.
      queryClient.invalidateQueries({ queryKey: ['clients'] });
      queryClient.invalidateQueries({ queryKey: ['dashboard'] });
    },
  });

  const templateMutation = useMutation({ mutationFn: clientImportService.downloadTemplate });

  const chooseFile = (chosen: File | null) => {
    if (!chosen) return;
    setFile(chosen);
    setPreview(null);
    setResult(null);
    previewMutation.reset();
    commitMutation.reset();
    previewMutation.mutate(chosen);
  };

  const startOver = () => {
    setFile(null);
    setPreview(null);
    setResult(null);
    setAcknowledged(false);
    previewMutation.reset();
    commitMutation.reset();
    if (fileInputRef.current) fileInputRef.current.value = '';
  };

  const skippedCount = preview ? preview.duplicateCount + preview.errorCount : 0;
  const needsAcknowledgement = skippedCount > 0;
  const canImport =
    !!preview &&
    !!file &&
    preview.readyCount > 0 &&
    (!needsAcknowledgement || acknowledged);

  const visibleRows = useMemo(() => {
    if (!preview) return [];
    if (filter === 'all') return preview.rows;
    const wanted: MemberImportRowStatus =
      filter === 'ready' ? 'Ready' : filter === 'duplicate' ? 'Duplicate' : 'Error';
    return preview.rows.filter((row) => row.status === wanted);
  }, [preview, filter]);

  return (
    <Box sx={{ maxWidth: 1100 }}>
      <Typography variant="h4" gutterBottom sx={{ fontWeight: 700 }}>
        Import existing members
      </Typography>
      <Typography variant="body1" color="text.secondary" sx={{ mb: 3 }}>
        Bring the gym&apos;s current member list in from a spreadsheet, once. Nothing is
        saved until you have seen exactly what will happen and confirmed it.
      </Typography>

      {/* ---------------------------------------------------------- upload */}
      {!result && (
        <Paper sx={{ p: 3, mb: 3 }}>
          <Stack
            direction={{ xs: 'column', sm: 'row' }}
            spacing={2}
            justifyContent="space-between"
            alignItems={{ sm: 'center' }}
            sx={{ mb: 2 }}
          >
            <Box>
              <Typography variant="h6" sx={{ fontWeight: 600 }}>
                1. Choose the file
              </Typography>
              <Typography variant="body2" color="text.secondary">
                Excel (.xlsx) or CSV. It needs a <strong>name</strong>,{' '}
                <strong>phone</strong>, <strong>package</strong> and{' '}
                <strong>membership end date</strong> for each member.
              </Typography>
            </Box>
            <Button
              variant="outlined"
              startIcon={<Download />}
              onClick={() => templateMutation.mutate()}
              disabled={templateMutation.isPending}
              sx={{ flexShrink: 0 }}
            >
              Download a template
            </Button>
          </Stack>

          <Box
            onDragOver={(e) => {
              e.preventDefault();
              setDragging(true);
            }}
            onDragLeave={() => setDragging(false)}
            onDrop={(e) => {
              e.preventDefault();
              setDragging(false);
              chooseFile(e.dataTransfer.files?.[0] ?? null);
            }}
            onClick={() => fileInputRef.current?.click()}
            sx={{
              border: '2px dashed',
              borderColor: dragging ? 'primary.main' : 'divider',
              bgcolor: dragging ? 'action.hover' : 'transparent',
              borderRadius: 2,
              p: 4,
              textAlign: 'center',
              cursor: 'pointer',
              transition: 'border-color 120ms, background-color 120ms',
              '&:hover': { borderColor: 'primary.main' },
            }}
          >
            <CloudUpload sx={{ fontSize: 44, color: 'primary.main', mb: 1 }} />
            <Typography variant="body1" sx={{ fontWeight: 600 }}>
              {file ? file.name : 'Drop the spreadsheet here, or click to choose it'}
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {file
                ? 'Choose another file to check a corrected version'
                : 'The file is only read — it is never changed'}
            </Typography>
            <input
              ref={fileInputRef}
              type="file"
              accept=".csv,.xlsx"
              hidden
              onChange={(e) => chooseFile(e.target.files?.[0] ?? null)}
            />
          </Box>

          {previewMutation.isPending && (
            <Stack direction="row" spacing={1.5} alignItems="center" sx={{ mt: 2 }}>
              <CircularProgress size={20} />
              <Typography variant="body2">Checking the file…</Typography>
            </Stack>
          )}

          {previewMutation.isError && (
            <Alert severity="error" sx={{ mt: 2 }}>
              <AlertTitle>That file could not be read</AlertTitle>
              {apiMessage(previewMutation.error, 'Something went wrong reading the file.')}
            </Alert>
          )}
        </Paper>
      )}

      {/* --------------------------------------------------------- preview */}
      {preview && (
        <Paper sx={{ p: 3, mb: 3 }}>
          <Typography variant="h6" sx={{ fontWeight: 600, mb: 0.5 }}>
            2. Check what will happen
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            {preview.totalRows} rows read from <strong>{preview.fileName}</strong>. Nothing
            has been saved yet.
          </Typography>

          <Stack direction="row" spacing={2} flexWrap="wrap" useFlexGap sx={{ mb: 2 }}>
            <SummaryTile
              count={preview.readyCount}
              label="will be added"
              color="success.main"
              icon={<CheckCircleOutline />}
            />
            <SummaryTile
              count={preview.duplicateCount}
              label="already members"
              color="warning.main"
              icon={<ContentCopy />}
            />
            <SummaryTile
              count={preview.errorCount}
              label="need fixing"
              color="error.main"
              icon={<ErrorOutline />}
            />
          </Stack>

          {preview.errorCount > 0 && (
            <Alert severity="warning" sx={{ mb: 2 }}>
              <AlertTitle>
                {preview.errorCount} row{preview.errorCount === 1 ? '' : 's'} cannot be
                imported
              </AlertTitle>
              Fix them in your own file and choose it again — the row numbers below match
              your spreadsheet. Or import the rest without them.
              {preview.availablePackages.length > 0 && (
                <Box component="span" sx={{ display: 'block', mt: 1 }}>
                  Packages you can use:{' '}
                  {preview.availablePackages.map((name) => (
                    <Chip key={name} label={name} size="small" sx={{ mr: 0.5 }} />
                  ))}
                </Box>
              )}
            </Alert>
          )}

          <ToggleButtonGroup
            size="small"
            exclusive
            value={filter}
            onChange={(_, value: RowFilter | null) => value && setFilter(value)}
            sx={{ mb: 2 }}
          >
            <ToggleButton value="all">All {preview.totalRows}</ToggleButton>
            <ToggleButton value="ready">Will be added {preview.readyCount}</ToggleButton>
            <ToggleButton value="duplicate">
              Already members {preview.duplicateCount}
            </ToggleButton>
            <ToggleButton value="error">Need fixing {preview.errorCount}</ToggleButton>
          </ToggleButtonGroup>

          <TableContainer sx={{ maxHeight: 460, border: 1, borderColor: 'divider', borderRadius: 1 }}>
            <Table stickyHeader size="small">
              <TableHead>
                <TableRow>
                  <TableCell sx={{ width: 64 }}>Row</TableCell>
                  <TableCell>Name</TableCell>
                  <TableCell>Phone</TableCell>
                  <TableCell>Package</TableCell>
                  <TableCell>Ends</TableCell>
                  <TableCell>Status</TableCell>
                  <TableCell>What happens</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {visibleRows.map((row) => (
                  <ImportRow key={row.rowNumber} row={row} />
                ))}
                {visibleRows.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={7} align="center" sx={{ py: 3 }}>
                      <Typography variant="body2" color="text.secondary">
                        No rows in this group.
                      </Typography>
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </TableContainer>

          <Divider sx={{ my: 3 }} />

          <Typography variant="h6" sx={{ fontWeight: 600, mb: 1 }}>
            3. Import them
          </Typography>

          <Alert severity="info" sx={{ mb: 2 }}>
            Imported members keep the end date they already had, and start with no payment
            history. Money the gym took before this system existed is not invented here —
            it would otherwise show up in every revenue report as income that never
            happened.
          </Alert>

          {needsAcknowledgement && (
            <FormControlLabel
              control={
                <Checkbox
                  checked={acknowledged}
                  onChange={(e) => setAcknowledged(e.target.checked)}
                />
              }
              label={`I understand ${skippedCount} row${
                skippedCount === 1 ? '' : 's'
              } will be skipped`}
              sx={{ display: 'block', mb: 1 }}
            />
          )}

          {commitMutation.isError && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {apiMessage(commitMutation.error, 'The import could not be completed.')}
            </Alert>
          )}

          <Stack direction="row" spacing={2}>
            <Button
              variant="contained"
              size="large"
              disabled={!canImport || commitMutation.isPending}
              onClick={() =>
                file &&
                commitMutation.mutate({
                  file,
                  fileHash: preview.fileHash,
                  acknowledgeSkipped: needsAcknowledgement,
                })
              }
            >
              {commitMutation.isPending
                ? 'Importing…'
                : `Import ${preview.readyCount} member${
                    preview.readyCount === 1 ? '' : 's'
                  }`}
            </Button>
            <Button variant="text" onClick={startOver} disabled={commitMutation.isPending}>
              Start again
            </Button>
          </Stack>

          {preview.readyCount === 0 && (
            <Typography variant="body2" color="text.secondary" sx={{ mt: 1.5 }}>
              There is nothing to import from this file yet.
            </Typography>
          )}
        </Paper>
      )}

      {/* ---------------------------------------------------------- result */}
      {result && (
        <Paper sx={{ p: 3 }}>
          <Stack direction="row" spacing={1.5} alignItems="center" sx={{ mb: 1 }}>
            <CheckCircleOutline color="success" sx={{ fontSize: 34 }} />
            <Typography variant="h5" sx={{ fontWeight: 700 }}>
              {result.importedCount} member{result.importedCount === 1 ? '' : 's'} imported
            </Typography>
          </Stack>

          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            They are on the <Link href="/admin/clients">Clients</Link> page now, with the
            end dates from your file. From here on, the system is the record — the
            spreadsheet is finished.
          </Typography>

          {result.skippedCount > 0 && (
            <Alert severity="warning" sx={{ mb: 2 }}>
              <AlertTitle>{result.skippedCount} rows were skipped</AlertTitle>
              These were not imported. If any of them are real members, fix them in your
              file and import just those.
              <TableContainer sx={{ mt: 1.5, maxHeight: 280 }}>
                <Table size="small" stickyHeader>
                  <TableHead>
                    <TableRow>
                      <TableCell sx={{ width: 64 }}>Row</TableCell>
                      <TableCell>Name</TableCell>
                      <TableCell>Why</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {result.skippedRows.map((row) => (
                      <TableRow key={row.rowNumber}>
                        <TableCell>{row.rowNumber}</TableCell>
                        <TableCell>{row.rawName || '—'}</TableCell>
                        <TableCell>{row.problems.join(' ')}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </TableContainer>
            </Alert>
          )}

          <Button variant="outlined" startIcon={<Refresh />} onClick={startOver}>
            Import another file
          </Button>
        </Paper>
      )}
    </Box>
  );
};

const SummaryTile = ({
  count,
  label,
  color,
  icon,
}: {
  count: number;
  label: string;
  color: string;
  icon: React.ReactNode;
}) => (
  <Paper
    variant="outlined"
    sx={{ px: 2.5, py: 1.5, minWidth: 168, display: 'flex', alignItems: 'center', gap: 1.5 }}
  >
    <Box sx={{ color, display: 'flex' }}>{icon}</Box>
    <Box>
      <Typography variant="h5" sx={{ fontWeight: 700, lineHeight: 1.1, color }}>
        {count}
      </Typography>
      <Typography variant="body2" color="text.secondary">
        {label}
      </Typography>
    </Box>
  </Paper>
);

const ImportRow = ({ row }: { row: MemberImportRow }) => {
  const style = STATUS_STYLE[row.status];
  const name = [row.firstName, row.lastName].filter(Boolean).join(' ') || row.rawName;

  return (
    <TableRow hover>
      <TableCell>{row.rowNumber}</TableCell>
      <TableCell>{name || '—'}</TableCell>
      <TableCell>{row.phoneNumber || '—'}</TableCell>
      <TableCell>
        {/* The raw value when it did not match, so the owner can see the typo. */}
        {row.packageName ?? row.rawPackage ?? '—'}
      </TableCell>
      <TableCell>
        {row.membershipEndDate ? (
          <Tooltip
            title={
              row.startDateWasDerived
                ? `Starts ${showDate(row.membershipStartDate)} — worked out from the package length, since your file did not say`
                : `Starts ${showDate(row.membershipStartDate)}`
            }
          >
            <span>{showDate(row.membershipEndDate)}</span>
          </Tooltip>
        ) : (
          row.rawEndDate || '—'
        )}
      </TableCell>
      <TableCell>
        <Stack direction="row" spacing={0.5} alignItems="center">
          <Chip size="small" label={style.label} color={style.color} variant="outlined" />
          {row.status === 'Ready' && row.membershipStatus && (
            <Chip size="small" label={row.membershipStatus} />
          )}
        </Stack>
      </TableCell>
      <TableCell sx={{ color: row.status === 'Error' ? 'error.main' : 'text.secondary' }}>
        {row.problems.length > 0 ? row.problems.join(' ') : 'Ready to add'}
      </TableCell>
    </TableRow>
  );
};

export default ImportMembersPage;
