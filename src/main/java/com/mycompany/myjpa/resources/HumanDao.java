package com.mycompany.myjpa.resources;

import jakarta.persistence.EntityManager;
import jakarta.persistence.EntityManagerFactory;
import jakarta.persistence.EntityTransaction;
import jakarta.persistence.Persistence;
import java.util.List;

public class HumanDao {
    private EntityManagerFactory enm = Persistence.createEntityManagerFactory("sa");
    EntityManager getEntityManager(){
        return enm.createEntityManager();
    }
    void save(Human human){
        EntityManager en = getEntityManager();
        EntityTransaction entx=en.getTransaction();
        try{
        entx.begin();
        en.persist(human);
        entx.commit();
        }catch(Exception e){
            if(entx!=null && entx.isActive())entx.rollback();
        }finally{
            if(en!=null && en.isOpen())en.close();
        }
    }//end save
    Human findHumanById(int id) {
        EntityManager en = getEntityManager();
        try {
            return en.find(Human.class, id);
        } catch (Exception e) {
            if (en != null && en.isOpen()) {
                en.close();
            }
            return null;
        }
    }
    List<Human> findAllHuman() {
        EntityManager en = getEntityManager();
        try {
            return en.createQuery("select h from Human h", Human.class).getResultList();
        } catch (Exception e) {
            if (en != null && en.isOpen()) {
                en.close();
            }
            return null;
        }
    }
}
