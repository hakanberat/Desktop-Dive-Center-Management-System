using System.Collections.ObjectModel;
using DiveCenterManager.Commands;
using DiveCenterManager.Helpers;
using DiveCenterManager.Models;
using DiveCenterManager.Services;

namespace DiveCenterManager.ViewModels;

public sealed class RoleOption : ViewModelBase
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    private bool _selected;
    public bool IsSelected { get => _selected; set => Set(ref _selected, value); }
}

public sealed class EditorViewModel : ViewModelBase
{
    public string Title { get; }
    public string Hint { get; set; } = "Fields marked * are required. Changes are saved together.";
    public object Entity { get; }
    public bool CanEdit { get; set; } = true;
    private bool _isSaving;
    public bool IsSaving
    {
        get => _isSaving;
        private set { Set(ref _isSaving, value); OnPropertyChanged(nameof(CanModify)); CancelCommand.RaiseCanExecuteChanged(); }
    }
    public bool CanModify => CanEdit && !IsSaving;
    public List<FieldViewModel> Fields { get; private set; } = [];
    public ObservableCollection<RoleOption> Roles { get; } = [];
    public bool HasRoles => Roles.Count > 0;
    public ChildCollectionViewModel? Children { get; set; }
    public string Summary { get; set; } = "";
    public string ReferenceLabel { get; set; } = "";
    public string ReferenceValue { get; set; } = "";
    public bool HasReference => ReferenceLabel.Length > 0;
    public string DetailsTabTitle => Entity is DiveLog ? "Shared dive information" : "Details";
    public string ChildrenTabTitle => Entity is DiveLog ? "Diver logs" : "Participants";
    public List<Transaction>? Payments { get; set; }
    public List<ColumnSpec> PaymentColumns => EntitySchema.Columns(typeof(Transaction));
    private string _error = "";
    public string Error { get => _error; set => Set(ref _error, value); }
    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand CancelCommand { get; }
    public event Action<bool>? CloseRequested;

    public EditorViewModel(string title, object entity, Func<object, Task> save)
    {
        Title = title; Entity = entity;
        SaveCommand = new AsyncRelayCommand(async () =>
        {
            IsSaving = true; Error = "";
            try
            {
                ApplyFields();
                if (Entity is Staff staff) staff.StaffRoles = Roles.Where(x => x.IsSelected).Select(x => new StaffStaffRole { StaffRoleId = x.Id, StaffId = staff.Id }).ToList();
                Children?.ApplyToParent(Entity);
                Validation.Check(Entity);
                await save(Entity);
            }
            finally { IsSaving = false; }
            CloseRequested?.Invoke(true);
        }, e => Error = ErrorMessages.Friendly(e), () => CanEdit);
        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(false), () => !IsSaving);
    }
    public async Task InitializeAsync(LookupService lookups, MasterDataService masters)
    {
        Fields = await FieldViewModel.ForEntityAsync(Entity, lookups);
        if (Entity is Staff staff)
        {
            foreach (var role in (await masters.ListAsync(typeof(StaffRole))).Cast<StaffRole>()
                .Where(x => x.IsActive || staff.StaffRoles.Any(r => r.StaffRoleId == x.Id)))
                Roles.Add(new RoleOption { Id = role.Id, Name = role.Name + (role.IsActive ? "" : " (inactive)"), IsSelected = staff.StaffRoles.Any(x => x.StaffRoleId == role.Id) });
        }
        // Customer selection copies the editable name/certification snapshot into the participant.
        if (Entity is ReservationParticipant or DiveOperationParticipant or DiverDiveLog)
        {
            var people = (await masters.ListAsync(typeof(Customer))).Cast<Customer>().ToDictionary(x => x.Id);
            var customerField = Fields.First(x => x.Key == "CustomerId");
            void SelectCustomer(bool copyFields)
            {
                var customer = customerField.SelectedValue is int id ? people.GetValueOrDefault(id) : null;
                if (Entity is ReservationParticipant participant) participant.Customer = customer;
                if (Entity is DiveOperationParticipant operationParticipant) operationParticipant.Customer = customer;
                if (Entity is DiverDiveLog personalLog) personalLog.Customer = customer;
                if (customer == null || !copyFields) return;
                foreach (var field in Fields.Where(x => x.Key is "FirstName" or "LastName" or "CertificationAgency" or "CertificationLevel" or "CertificationNumber"))
                    field.SetValue(typeof(Customer).GetProperty(field.Key)!.GetValue(customer));
                Fields.FirstOrDefault(x => x.Key == "ParticipantName")?.SetValue(customer.FullName);
            }
            SelectCustomer(false);
            customerField.ValueChanged += (_, _) => SelectCustomer(true);
        }
    }
    public void ApplyFields()
    {
        var values = new List<(System.Reflection.PropertyInfo Property, object? Value)>();
        var errors = new List<string>();
        foreach (var field in Fields)
        {
            try
            {
                var value = field.Read();
                if (Entity is Currency && field.Key == "Code" && value is string code) { value = code.Trim().ToUpperInvariant(); field.SetValue(value); }
                var propertyErrors = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
                var context = new System.ComponentModel.DataAnnotations.ValidationContext(Entity) { MemberName = field.Key, DisplayName = field.Label.TrimEnd(' ', '*') };
                System.ComponentModel.DataAnnotations.Validator.TryValidateProperty(value, context, propertyErrors);
                if (propertyErrors.Count > 0)
                {
                    field.Error = string.Join(Environment.NewLine, propertyErrors.Select(x => x.ErrorMessage));
                    throw new System.ComponentModel.DataAnnotations.ValidationException(field.Error);
                }
                values.Add((Entity.GetType().GetProperty(field.Key)!, value));
            }
            catch (System.ComponentModel.DataAnnotations.ValidationException e) { errors.Add(e.Message); }
        }
        Validation.Require(errors.Count == 0, string.Join(Environment.NewLine, errors));
        foreach (var (property, value) in values) property.SetValue(Entity, value);
        if (Entity is Currency currency) currency.Code = currency.Code.Trim().ToUpperInvariant();
    }
}

public sealed class ChildCollectionViewModel : ViewModelBase
{
    public string Title { get; }
    public string Hint { get; }
    public Type EntityType { get; }
    public bool CanEdit { get; set; } = true;
    public List<ColumnSpec> Columns => EntitySchema.Columns(EntityType);
    public ObservableCollection<object> Items { get; }
    public string CountSummary => $"{Items.Count} {(EntityType == typeof(DiverDiveLog) ? "diver log(s)" : "participant(s)")}";
    public string AddLabel => EntityType == typeof(DiverDiveLog) ? "+ Add diver log" : "+ Add participant";
    public string EditLabel => EntityType == typeof(DiverDiveLog) ? "Edit diver log" : "Open / edit participant";
    public string RemoveLabel => EntityType == typeof(DiverDiveLog) ? "Remove diver log" : "Remove participant";
    private readonly Func<bool, Task<EditorViewModel>> _createEditor;
    private object? _selected;
    public object? Selected { get => _selected; set { Set(ref _selected, value); EditCommand.RaiseCanExecuteChanged(); RemoveCommand.RaiseCanExecuteChanged(); } }
    public AsyncRelayCommand AddCommand { get; }
    public AsyncRelayCommand EditCommand { get; }
    public RelayCommand RemoveCommand { get; }
    private string _error = "";
    public string Error { get => _error; set => Set(ref _error, value); }
    public ChildCollectionViewModel(string title, Type entityType, IEnumerable<object> items, LookupService lookups, MasterDataService masters, DialogService dialogs)
    {
        Title = title; EntityType = entityType; Items = new ObservableCollection<object>(items);
        Items.CollectionChanged += (_, _) => { OnPropertyChanged(nameof(CountSummary)); if (Selected != null && !Items.Contains(Selected)) Selected = null; };
        Hint = entityType == typeof(DiverDiveLog) ? "Shared depth and bottom time apply when personal overrides are blank." : "Link an existing customer or enter a temporary visitor. Certification is optional.";
        async Task<EditorViewModel> CreateEditorAsync(bool create)
        {
            var selected = Selected;
            var entity = create ? Activator.CreateInstance(entityType)! : EntityCopy.Copy(selected ?? throw new System.ComponentModel.DataAnnotations.ValidationException("Select a participant to edit."));
            var editor = new EditorViewModel(create ? AddLabel.TrimStart('+', ' ') : EditLabel, entity, changed =>
            {
                Validation.Check(changed);
                if (changed is ReservationParticipant participant)
                    OperationalRules.UniqueCustomers(Items.Where(x => !ReferenceEquals(x, selected) || create).Cast<ReservationParticipant>().Select(x => x.CustomerId).Append(participant.CustomerId));
                if (changed is DiverDiveLog personalLog)
                    OperationalRules.UniqueCustomers(Items.Where(x => !ReferenceEquals(x, selected) || create).Cast<DiverDiveLog>().Select(x => x.CustomerId).Append(personalLog.CustomerId));
                if (changed is DiveOperationParticipant operationParticipant)
                {
                    var others = Items.Where(x => !ReferenceEquals(x, selected) || create).Cast<DiveOperationParticipant>().ToList();
                    OperationalRules.UniqueCustomers(others.Select(x => x.CustomerId).Append(operationParticipant.CustomerId));
                    Validation.Require(!operationParticipant.ReservationParticipantId.HasValue || others.All(x => x.ReservationParticipantId != operationParticipant.ReservationParticipantId),
                        "This reservation participant is already included in the operation.");
                }
                if (create) Items.Add(changed);
                else
                {
                    Validation.Require(Items.Contains(selected!), "The selected participant was removed. Open the editor again.");
                    Items[Items.IndexOf(selected!)] = changed;
                }
                Selected = changed;
                return Task.CompletedTask;
            });
            editor.CanEdit = CanEdit;
            await editor.InitializeAsync(lookups, masters);
            if (entity is DiveOperationParticipant person)
            {
                editor.ReferenceLabel = "Participant source";
                editor.ReferenceValue = person.ReservationParticipantId.HasValue ? "Copied from the linked reservation" : "Added directly to this operation";
            }
            return editor;
        }
        _createEditor = CreateEditorAsync;
        async Task EditAsync(bool create) => dialogs.Edit(await CreateEditorAsync(create));
        AddCommand = new AsyncRelayCommand(() => EditAsync(true), e => Error = ErrorMessages.Friendly(e), () => CanEdit);
        EditCommand = new AsyncRelayCommand(() => EditAsync(false), e => Error = ErrorMessages.Friendly(e), () => Selected != null);
        RemoveCommand = new RelayCommand(() => { if (Selected != null) Items.Remove(Selected); }, () => CanEdit && Selected != null);
    }
    public Task<EditorViewModel> CreateEditorAsync(bool create) => _createEditor(create);
    public void ApplyToParent(object parent)
    {
        if (parent is Reservation reservation) { reservation.Participants = Items.Cast<ReservationParticipant>().ToList(); reservation.NumberOfParticipants = Items.Count; }
        if (parent is DiveOperation operation)
        {
            Validation.Require(Items.Count > 0, "Add at least one operation participant.");
            operation.Participants = Items.Cast<DiveOperationParticipant>().ToList();
        }
        if (parent is DiveLog log) log.DiverLogs = Items.Cast<DiverDiveLog>().ToList();
    }
}
