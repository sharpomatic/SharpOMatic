import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SettingsService } from '../../services/settings.service';
import { ServerRepositoryService } from '../../services/server.repository.service';
import { Setting } from './interfaces/setting';
import { SettingType } from '../../enumerations/setting-type';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './settings.component.html',
  styleUrls: ['./settings.component.scss']
})
export class SettingsComponent implements OnInit {
  private readonly settingsService = inject(SettingsService);
  private readonly serverRepository = inject(ServerRepositoryService);

  apiUrlValue = this.settingsService.apiUrl();
  settings: Setting[] = [];
  readonly settingType = SettingType;

  ngOnInit(): void {
    this.loadSettings();
  }

  onApiUrlChange(value: string): void {
    this.settingsService.setApiUrl(value);
    this.apiUrlValue = this.settingsService.apiUrl();
  }

  onStringBlur(setting: Setting): void {
    const trimmed = setting.valueString?.trim();
    setting.valueString = trimmed && trimmed.length ? trimmed : null;
    this.saveSetting(setting);
  }

  onBooleanChange(setting: Setting, value: boolean): void {
    setting.valueBoolean = value;
    this.saveSetting(setting);
  }

  onIntegerBlur(setting: Setting): void {
    if (setting.valueInteger == null) {
      setting.valueInteger = null;
      this.saveSetting(setting);
      return;
    }

    if (!Number.isFinite(setting.valueInteger)) {
      return;
    }

    setting.valueInteger = Math.trunc(setting.valueInteger);
    this.saveSetting(setting);
  }

  onDoubleBlur(setting: Setting): void {
    if (setting.valueDouble == null) {
      setting.valueDouble = null;
      this.saveSetting(setting);
      return;
    }

    if (!Number.isFinite(setting.valueDouble)) {
      return;
    }

    this.saveSetting(setting);
  }

  settingInputId(setting: Setting): string {
    return `setting-${setting.settingId}`;
  }

  private loadSettings(): void {
    this.serverRepository.getSettings().subscribe(settings => {
      this.settings = settings.filter(setting => setting.userEditable);
    });
  }

  private saveSetting(setting: Setting): void {
    this.serverRepository.upsertSetting(setting).subscribe();
  }
}
