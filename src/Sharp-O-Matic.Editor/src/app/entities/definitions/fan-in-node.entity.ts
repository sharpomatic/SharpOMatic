import { ConnectorEntity } from './connector.entity';
import { NodeEntity, NodeSnapshot } from './node.entity';
import { NodeType } from '../enumerations/node-type';

export interface FanInNodeSnapshot extends NodeSnapshot {
}

export class FanInNodeEntity extends NodeEntity<FanInNodeSnapshot> {

    constructor(snapshot: FanInNodeSnapshot) {
        super(snapshot);
    }

    public override toSnapshot(): FanInNodeSnapshot {
        return {
            ...super.toNodeSnapshot(),
        };
    }

    public static fromSnapshot(snapshot: FanInNodeSnapshot): FanInNodeEntity {
        return new FanInNodeEntity(snapshot);
    }

    public static override defaultSnapshot(): FanInNodeSnapshot {
        return {
            ...NodeEntity.defaultSnapshot(),
            nodeType: NodeType.FanIn,
            title: 'Fan In',
            inputs: [ConnectorEntity.defaultSnapshot()],
            outputs: [ConnectorEntity.defaultSnapshot()],
        }
    }

    public static create(top: number, left: number): FanInNodeEntity {
        return new FanInNodeEntity({
            ...FanInNodeEntity.defaultSnapshot(),
            top,
            left,
        });
    }
}
