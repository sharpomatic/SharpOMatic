import { Injectable, ComponentFactoryResolver, ApplicationRef, Injector, EmbeddedViewRef, Type, InjectionToken } from '@angular/core';

export const DIALOG_DATA = new InjectionToken<any>('DIALOG_DATA');

@Injectable({
  providedIn: 'root'
})
export class DialogService {
  private componentRef: any;

  constructor(
    private componentFactoryResolver: ComponentFactoryResolver,
    private appRef: ApplicationRef,
    private injector: Injector
  ) { }

  open(component: Type<any>, options?: any) {
    if (this.componentRef) {
      return;
    }

    const componentFactory = this.componentFactoryResolver.resolveComponentFactory(component);

    const dialogInjector = Injector.create({
      providers: [{ provide: DIALOG_DATA, useValue: options }],
      parent: this.injector
    });

    this.componentRef = componentFactory.create(dialogInjector);
    this.appRef.attachView(this.componentRef.hostView);

    const domElem = (this.componentRef.hostView as EmbeddedViewRef<any>).rootNodes[0] as HTMLElement;
    document.body.appendChild(domElem);

    this.componentRef.instance.close.subscribe(() => {
      this.close();
    });
  }

  close(): void {
    if (!this.componentRef) {
      return;
    }

    this.appRef.detachView(this.componentRef.hostView);
    this.componentRef.destroy();
    this.componentRef = null;
  }
}
