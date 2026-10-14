namespace DiveCenterManager.Models;

public enum CertificationAgency { PADI, SSI, CMAS, RAID, SDI, NAUI, TDI, Other }
public enum ActivityType { TryDive, GuidedDive, FunDive, TrainingDive, Course, Specialty, PrivateDive, EquipmentRental, Other }
public enum ReservationStatus { Pending, Confirmed, Completed, Cancelled, NoShow }
public enum OperationStatus { Planned, InProgress, Completed, Cancelled }
public enum ParticipantRole { Diver, Student, Instructor, Divemaster, DSDParticipant }
public enum GasType { Air, Nitrox, Other }
public enum EquipmentType { BCD, Regulator, Wetsuit, Mask, Fins, Tank, WeightBelt, Computer, Other }
public enum EquipmentStatus { Available, InUse, Maintenance, Retired }
public enum CategoryType { Income, Expense, Both }
public enum TransactionType { Income, Expense }
