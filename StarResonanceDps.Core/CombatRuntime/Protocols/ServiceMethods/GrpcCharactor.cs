// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.CombatRuntime.Protocols.ServiceMethods;

public enum GrpcCharactorNtf
{
    Login = 0x1,
    CreateChar = 0x2,
    SelectChar = 0x3,
    DeleteChar = 0x4,
    Reconnect = 0x5,
    ExitGame = 0x6,
    ReportMSdk = 0xA,
    GetFaceUpToken = 0x11,
    UploadFaceSuccess = 0x12,
    GetFaceUploadData = 0x13,
    GetFaceDataUrl = 0x14,
    CancelDeleteChar = 0x16,
    PrivilegeActivate = 0x17,
    TakeAwardByCdKey = 0x19,
    SyncLanguage = 0x1D,
}
