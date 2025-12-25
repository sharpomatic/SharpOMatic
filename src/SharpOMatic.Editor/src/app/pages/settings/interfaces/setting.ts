import { SettingType } from '../../../enumerations/setting-type';

export interface Setting {
  settingId: string;
  name: string;
  displayName: string;
  settingType: SettingType;
  valueString?: string | null;
  valueBoolean?: boolean | null;
  valueInteger?: number | null;
  valueDouble?: number | null;
}
