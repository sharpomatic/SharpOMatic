import {
  Injectable,
  ComponentFactoryResolver,
  ApplicationRef,
  Injector,
  EmbeddedViewRef,
  Type,
  InjectionToken,
} from '@angular/core';

export const DIALOG_DATA = new InjectionToken<any>('DIALOG_DATA');

@Injectable({
  providedIn: 'root',
})
export class DialogService {
  private componentRefs: any[] = [];

  constructor(
    private componentFactoryResolver: ComponentFactoryResolver,
    private appRef: ApplicationRef,
    private injector: Injector,
  ) {}

  open(component: Type<any>, options?: any) {
    const allowStack = options?.allowStack === true;
    if (this.componentRefs.length > 0 && !allowStack) {
      return;
    }

    const componentFactory =
      this.componentFactoryResolver.resolveComponentFactory(component);

    const dialogInjector = Injector.create({
      providers: [{ provide: DIALOG_DATA, useValue: options }],
      parent: this.injector,
    });

    const componentRef = componentFactory.create(dialogInjector);
    this.componentRefs.push(componentRef);
    this.appRef.attachView(componentRef.hostView);

    const domElem = (componentRef.hostView as EmbeddedViewRef<any>)
      .rootNodes[0] as HTMLElement;
    document.body.appendChild(domElem);

    componentRef.instance.close.subscribe(() => {
      this.close(componentRef);
    });
  }

  close(componentRef?: any): void {
    const ref =
      componentRef ?? this.componentRefs[this.componentRefs.length - 1];
    if (!ref) {
      return;
    }

    this.appRef.detachView(ref.hostView);
    ref.destroy();
    const index = this.componentRefs.indexOf(ref);
    if (index >= 0) {
      this.componentRefs.splice(index, 1);
    }
  }
}
