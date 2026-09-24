import {
  MobileLoginRequestSchema,
  ActivateAccountRequestSchema,
  MyDeviceDtoSchema,
  ReportMyDeviceIncidentRequestSchema,
} from '../src/lib/validation/schemas';

describe('Zod Validation Schemas', () => {
  describe('MobileLoginRequestSchema', () => {
    it('accepts valid credentials', () => {
      const valid = {
        organizationCode: 'sentinel-corp',
        email: 'employee@sentinel.local',
        password: 'ValidSecretPassword123!',
      };
      expect(() => MobileLoginRequestSchema.parse(valid)).not.toThrow();
    });

    it('rejects invalid email or empty fields', () => {
      expect(() =>
        MobileLoginRequestSchema.parse({
          organizationCode: 's',
          email: 'not-an-email',
          password: '',
        })
      ).toThrow();
    });
  });

  describe('ActivateAccountRequestSchema', () => {
    it('validates matching passwords with minimum length 12', () => {
      const valid = {
        token: 'token-1234567890123456',
        password: 'SuperSecurePassword123!',
        confirmPassword: 'SuperSecurePassword123!',
      };
      expect(() => ActivateAccountRequestSchema.parse(valid)).not.toThrow();
    });

    it('rejects passwords shorter than 12 characters', () => {
      const invalid = {
        token: 'token-1234567890123456',
        password: 'short',
        confirmPassword: 'short',
      };
      expect(() => ActivateAccountRequestSchema.parse(invalid)).toThrow();
    });

    it('rejects mismatched passwords', () => {
      const invalid = {
        token: 'token-1234567890123456',
        password: 'SuperSecurePassword123!',
        confirmPassword: 'DifferentPassword123!',
      };
      expect(() => ActivateAccountRequestSchema.parse(invalid)).toThrow();
    });
  });

  describe('MyDeviceDtoSchema', () => {
    it('parses valid MyDevice response structure', () => {
      const sample = {
        device: {
          id: '12345678-1234-1234-1234-123456789012',
          name: 'WS-EMPLOYEE-01',
          isOnline: true,
          osVersion: 'Windows 11 Enterprise',
          agentVersion: '0.1.0',
        },
        appliedPolicy: 'Standard Endpoint Protection',
        latestTelemetry: {
          id: 't-1',
          deviceId: '12345678-1234-1234-1234-123456789012',
          cpuPercent: 24.5,
          ramPercent: 62.0,
          diskPercent: 45.8,
          createdAt: new Date().toISOString(),
        },
        incidents: [],
        privacyManifest: {
          collectedTechnicalData: ['CPU', 'RAM'],
          strictlyProhibitedData: ['GPS', 'Microphone'],
          agentPermissions: ['Read system stats'],
          dataRetentionDays: 90,
        },
        assignedAt: new Date().toISOString(),
      };
      const parsed = MyDeviceDtoSchema.parse(sample);
      expect(parsed.device.name).toBe('WS-EMPLOYEE-01');
      expect(parsed.privacyManifest.dataRetentionDays).toBe(90);
    });
  });

  describe('ReportMyDeviceIncidentRequestSchema', () => {
    it('validates incident title and allowed severities', () => {
      const valid = {
        title: 'LAN cable broken',
        description: 'Unable to ping local gateway',
        severity: 'High',
      };
      expect(() => ReportMyDeviceIncidentRequestSchema.parse(valid)).not.toThrow();
    });

    it('rejects title shorter than 3 characters', () => {
      expect(() =>
        ReportMyDeviceIncidentRequestSchema.parse({
          title: 'ab',
        })
      ).toThrow();
    });
  });
});
