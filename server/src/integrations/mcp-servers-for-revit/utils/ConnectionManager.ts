import { AsyncLocalStorage } from 'node:async_hooks';
import type { RevitBridge } from '../../../bridge.js';
export interface Invocation {
  bridge: RevitBridge; documentId: string; operationId?: string; response?: unknown; error?: unknown;
}
export const invocation = new AsyncLocalStorage<Invocation>();
export async function withRevitConnection<T>(operation: (client: { sendCommand(name: string, parameters: any): Promise<any> }) => Promise<T>): Promise<T> {
  const context = invocation.getStore();
  if (!context) throw new Error('Upstream tool must run through TYBIM bridge');
  return operation({ sendCommand: async (name, parameters) => {
    try {
      context.response = await context.bridge.call('upstream.' + name, {
        ...parameters, documentId: context.documentId, operationId: context.operationId
      }, 60000);
      return context.response;
    } catch (error) { context.error = error; throw error; }
  } });
}
